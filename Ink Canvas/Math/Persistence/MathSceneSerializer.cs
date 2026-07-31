using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Ink_Canvas.Mathematics.Models;
using Ink_Canvas.Mathematics.Services;

namespace Ink_Canvas.Mathematics.Persistence
{
    public static class MathSceneSerializer
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static string Serialize(MathScene scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", scene.SchemaVersion);
                writer.WritePropertyName("objects");
                writer.WriteStartArray();

                for (var i = 0; i < scene.Objects.Count; i++)
                {
                    var mathObject = scene.Objects[i] ?? throw new InvalidOperationException("Math scene contains a null object.");
                    JsonSerializer.Serialize(writer, mathObject, mathObject.GetType(), JsonOptions);
                }

                writer.WriteEndArray();
                writer.WritePropertyName("constraints");
                JsonSerializer.Serialize(writer, scene.Constraints ?? new List<MathConstraint>(), JsonOptions);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static MathSceneLoadResult Deserialize(string json)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(json))
            {
                issues.Add("Math scene data is empty.");
                return new MathSceneLoadResult(new MathScene(), issues);
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    issues.Add("Math scene root must be an object.");
                    return new MathSceneLoadResult(new MathScene(), issues);
                }

                var schemaVersion = ReadSchemaVersion(root);
                if (schemaVersion < 1 || schemaVersion > MathScene.CurrentSchemaVersion)
                {
                    issues.Add($"Unsupported math scene schema version: {schemaVersion}.");
                    return new MathSceneLoadResult(new MathScene(), issues);
                }

                var scene = new MathScene { SchemaVersion = schemaVersion };
                if (!root.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Array)
                {
                    issues.Add("Math scene objects collection is missing or invalid.");
                    return new MathSceneLoadResult(scene, issues);
                }

                var service = new MathSceneService(scene);
                var index = 0;
                foreach (var element in objects.EnumerateArray())
                {
                    TryAddObject(element, index, service, issues);
                    index++;
                }

                MathReferenceService.Synchronize(scene);
                ReadConstraints(root, scene, issues);
                return new MathSceneLoadResult(scene, issues);
            }
            catch (JsonException exception)
            {
                issues.Add($"Math scene JSON is invalid: {exception.Message}");
                return new MathSceneLoadResult(new MathScene(), issues);
            }
        }

        private static void ReadConstraints(
            JsonElement root,
            MathScene scene,
            ICollection<string> issues)
        {
            if (!root.TryGetProperty("constraints", out var constraints))
                return;
            if (constraints.ValueKind != JsonValueKind.Array)
            {
                issues.Add("Math scene constraints collection is invalid.");
                return;
            }

            var index = 0;
            foreach (var element in constraints.EnumerateArray())
            {
                try
                {
                    var constraint = element.Deserialize<MathConstraint>(JsonOptions);
                    MathConstraintService.Add(scene, constraint);
                }
                catch (Exception exception) when (
                    exception is JsonException ||
                    exception is ArgumentException ||
                    exception is InvalidOperationException)
                {
                    issues.Add($"Math constraint at index {index} was skipped: {exception.Message}");
                }

                index++;
            }
        }

        private static int ReadSchemaVersion(JsonElement root)
        {
            return root.TryGetProperty("schemaVersion", out var property) &&
                   property.ValueKind == JsonValueKind.Number &&
                   property.TryGetInt32(out var version)
                ? version
                : 0;
        }

        private static void TryAddObject(
            JsonElement element,
            int index,
            MathSceneService service,
            ICollection<string> issues)
        {
            try
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !element.TryGetProperty("type", out var typeProperty) ||
                    typeProperty.ValueKind != JsonValueKind.Number ||
                    !typeProperty.TryGetInt32(out var typeValue) ||
                    !Enum.IsDefined(typeof(MathObjectType), typeValue))
                {
                    issues.Add($"Math object at index {index} has an unknown type.");
                    return;
                }

                var type = (MathObjectType)typeValue;
                MathObject mathObject = type switch
                {
                    MathObjectType.Point => element.Deserialize<PointObject>(JsonOptions),
                    MathObjectType.Segment => element.Deserialize<SegmentObject>(JsonOptions),
                    MathObjectType.Circle => element.Deserialize<CircleObject>(JsonOptions),
                    MathObjectType.TextLabel => element.Deserialize<TextLabelObject>(JsonOptions),
                    MathObjectType.Line => element.Deserialize<LineObject>(JsonOptions),
                    MathObjectType.Ray => element.Deserialize<RayObject>(JsonOptions),
                    MathObjectType.AngleMeasurement => element.Deserialize<AngleMeasurementObject>(JsonOptions),
                    MathObjectType.Function => element.Deserialize<FunctionObject>(JsonOptions),
                    MathObjectType.Solid => element.Deserialize<SolidObject>(JsonOptions),
                    MathObjectType.CoordinatePlane => element.Deserialize<CoordinatePlaneObject>(JsonOptions),
                    MathObjectType.Triangle => element.Deserialize<TriangleObject>(JsonOptions),
                    _ => null
                };

                if (mathObject == null)
                {
                    issues.Add($"Math object at index {index} could not be read.");
                    return;
                }

                service.Add(mathObject);
            }
            catch (Exception exception) when (
                exception is JsonException ||
                exception is ArgumentException ||
                exception is InvalidOperationException)
            {
                issues.Add($"Math object at index {index} was skipped: {exception.Message}");
            }
        }
    }
}

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ink_Canvas.Ink
{
    // A broken optional calibration must not discard the rest of a settings profile.
    internal sealed class TouchCalibrationRecordConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanConvert(Type objectType) => objectType == typeof(TouchCalibrationRecord);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            if (token.Type != JTokenType.Object) return null;
            try
            {
                var record = token.ToObject<TouchCalibrationRecord>(serializer);
                return record?.IsValid() == true ? record : null;
            }
            catch (JsonException) { return null; }
            catch (FormatException) { return null; }
            catch (OverflowException) { return null; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => throw new NotSupportedException();
    }
}

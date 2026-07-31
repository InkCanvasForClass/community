using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ink_Canvas.Helpers;
using Ink_Canvas.Mathematics.Models;
using Ink_Canvas.Mathematics.Persistence;
using Ink_Canvas.Mathematics.Services;

namespace InkCanvas.Math.Tests
{
    internal static class Program
    {
        private static int _passed;

        private static void Main()
        {
            Run(nameof(NewSceneUsesCurrentSchema), NewSceneUsesCurrentSchema);
            Run(nameof(AddAndFindObject), AddAndFindObject);
            Run(nameof(DuplicateIdIsRejected), DuplicateIdIsRejected);
            Run(nameof(InvalidCircleDoesNotChangeScene), InvalidCircleDoesNotChangeScene);
            Run(nameof(RemoveUnknownObjectDoesNotChangeScene), RemoveUnknownObjectDoesNotChangeScene);
            Run(nameof(SceneRoundTripsAllBaseObjects), SceneRoundTripsAllBaseObjects);
            Run(nameof(UnknownObjectIsIsolated), UnknownObjectIsIsolated);
            Run(nameof(InvalidObjectIsIsolated), InvalidObjectIsIsolated);
            Run(nameof(MalformedSceneReturnsEmptyResult), MalformedSceneReturnsEmptyResult);
            Run(nameof(HitTestPrefersTopmostObject), HitTestPrefersTopmostObject);
            Run(nameof(TranslateMovesSegmentEndpoints), TranslateMovesSegmentEndpoints);
            Run(nameof(SceneFileStoreRoundTripsAtomically), SceneFileStoreRoundTripsAtomically);
            Run(nameof(LineIntersectionRespectsSegmentBounds), LineIntersectionRespectsSegmentBounds);
            Run(nameof(LineCircleIntersectionReturnsTwoPoints), LineCircleIntersectionReturnsTwoPoints);
            Run(nameof(TangentCirclesReturnOnePoint), TangentCirclesReturnOnePoint);
            Run(nameof(AngleMeasurementReturnsRightAngle), AngleMeasurementReturnsRightAngle);
            Run(nameof(SnapFindsSegmentMidpoint), SnapFindsSegmentMidpoint);
            Run(nameof(SnapFindsIntersectionPoint), SnapFindsIntersectionPoint);
            Run(nameof(SnapReturnsPointReference), SnapReturnsPointReference);
            Run(nameof(SnapProjectsToEdgesLinesAndSolids), SnapProjectsToEdgesLinesAndSolids);
            Run(nameof(SnapReturnsSolidAttachmentCoordinates), SnapReturnsSolidAttachmentCoordinates);
            Run(nameof(SolidAttachmentsFollowParentAndRoundTrip), SolidAttachmentsFollowParentAndRoundTrip);
            Run(nameof(StrictSolidSphereConstructionsRespectGeometry), StrictSolidSphereConstructionsRespectGeometry);
            Run(nameof(TriangleCirclesFollowTheirTriangle), TriangleCirclesFollowTheirTriangle);
            Run(nameof(MovingPointUpdatesReferencedSegment), MovingPointUpdatesReferencedSegment);
            Run(nameof(RemovingPointDetachesReferencedSegment), RemovingPointDetachesReferencedSegment);
            Run(nameof(PointReferencesRoundTrip), PointReferencesRoundTrip);
            Run(nameof(HorizontalConstraintUpdatesSegment), HorizontalConstraintUpdatesSegment);
            Run(nameof(ConflictingConstraintsRestoreScene), ConflictingConstraintsRestoreScene);
            Run(nameof(ConstraintsRoundTrip), ConstraintsRoundTrip);
            Run(nameof(ParallelAndPerpendicularConstraintsRoundTrip), ParallelAndPerpendicularConstraintsRoundTrip);
            Run(nameof(ThreeHundredObjectInteractionCompletesWithinBudget), ThreeHundredObjectInteractionCompletesWithinBudget);
            Run(nameof(ExpressionParserSupportsRequiredOperations), ExpressionParserSupportsRequiredOperations);
            Run(nameof(ExpressionParserRejectsUnsafeInput), ExpressionParserRejectsUnsafeInput);
            Run(nameof(FunctionSamplerDrawsRequiredExamples), FunctionSamplerDrawsRequiredExamples);
            Run(nameof(FunctionAnalysisReturnsCoordinateProperties), FunctionAnalysisReturnsCoordinateProperties);
            Run(nameof(FunctionSamplerSplitsDiscontinuity), FunctionSamplerSplitsDiscontinuity);
            Run(nameof(FunctionRoundTripsEditableExpression), FunctionRoundTripsEditableExpression);
            Run(nameof(MovingFunctionMovesItsCoordinateFrame), MovingFunctionMovesItsCoordinateFrame);
            Run(nameof(MovingLegacyFunctionFindsContainingCoordinateFrame), MovingLegacyFunctionFindsContainingCoordinateFrame);
            Run(nameof(RotatedFunctionCanBeSelected), RotatedFunctionCanBeSelected);
            Run(nameof(ContrastStrokeColorAdaptsToBackground), ContrastStrokeColorAdaptsToBackground);
            Run(nameof(TwentyFunctionCurvesCompleteWithinBudget), TwentyFunctionCurvesCompleteWithinBudget);
            Run(nameof(FunctionIntersectionsAreDetected), FunctionIntersectionsAreDetected);
            Run(nameof(FunctionIntersectionsRequireSharedCoordinateFrame), FunctionIntersectionsRequireSharedCoordinateFrame);
            Run(nameof(FunctionIntersectionsAreCachedUntilInputsChange), FunctionIntersectionsAreCachedUntilInputsChange);
            Run(nameof(FunctionSamplesAreReusedUntilInputsChange), FunctionSamplesAreReusedUntilInputsChange);
            Run(nameof(CachedFunctionSamplingAvoidsRepeatedWork), CachedFunctionSamplingAvoidsRepeatedWork);
            Run(nameof(CoordinatePlaneClickUsesDefaultSizeAndDragUsesCustomSize), CoordinatePlaneClickUsesDefaultSizeAndDragUsesCustomSize);
            Run(nameof(CoordinatePlaneCanBeInsertedMovedResizedAndSaved), CoordinatePlaneCanBeInsertedMovedResizedAndSaved);
            Run(nameof(CoordinatePlaneStaysBehindLaterMathObjects), CoordinatePlaneStaysBehindLaterMathObjects);
            Run(nameof(AllSolidTypesBuildAndProject), AllSolidTypesBuildAndProject);
            Run(nameof(SolidDefaultsUseClassroomIsometricView), SolidDefaultsUseClassroomIsometricView);
            Run(nameof(SolidAxesProjectToDistinctDirections), SolidAxesProjectToDistinctDirections);
            Run(nameof(SolidProjectionKeepsParallelEdgesParallel), SolidProjectionKeepsParallelEdgesParallel);
            Run(nameof(FrontViewCollapsesDepthAxis), FrontViewCollapsesDepthAxis);
            Run(nameof(SolidViewModeRoundTripsAndLegacyScenesDefaultToProjection), SolidViewModeRoundTripsAndLegacyScenesDefaultToProjection);
            Run(nameof(CurvedSolidsUseOnlyKeyConstructionLines), CurvedSolidsUseOnlyKeyConstructionLines);
            Run(nameof(SolidInteriorCanBeSelected), SolidInteriorCanBeSelected);
            Run(nameof(SolidCanStretchHorizontallyAndVertically), SolidCanStretchHorizontallyAndVertically);
            Run(nameof(SolidRotationSnapsToParallelAndPerpendicularViews), SolidRotationSnapsToParallelAndPerpendicularViews);
            Run(nameof(SolidRotationPreservesMeasurements), SolidRotationPreservesMeasurements);
            Run(nameof(SolidRoundTripsCameraAndDimensions), SolidRoundTripsCameraAndDimensions);
            Run(nameof(VersionOneSceneRemainsReadable), VersionOneSceneRemainsReadable);
            Run(nameof(VersionTwoSceneRemainsReadable), VersionTwoSceneRemainsReadable);
            Run(nameof(FutureSceneVersionIsRejected), FutureSceneVersionIsRejected);
            Run(nameof(FullSceneFileRoundTripPreservesEveryLayer), FullSceneFileRoundTripPreservesEveryLayer);
            Run(nameof(TimeMachineUndoRedoRestoresMathSnapshots), TimeMachineUndoRedoRestoresMathSnapshots);
            Run(nameof(TimeMachineDropsRedoBranchAfterMathEdit), TimeMachineDropsRedoBranchAfterMathEdit);
            Run(nameof(GridCoordinatesUseCanvasCenterAndYAxisUp), GridCoordinatesUseCanvasCenterAndYAxisUp);
            Console.WriteLine($"Math scene contract tests passed: {_passed}.");
        }

        private static void NewSceneUsesCurrentSchema()
        {
            var scene = new MathScene();
            Equal(MathScene.CurrentSchemaVersion, scene.SchemaVersion);
            Equal(0, scene.Objects.Count);
        }

        private static void AddAndFindObject()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var point = new PointObject { Position = new MathPoint(12, 34) };

            service.Add(point);

            Equal(1, scene.Objects.Count);
            Same(point, service.Find(point.Id));
        }

        private static void DuplicateIdIsRejected()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var first = new PointObject();
            var duplicate = new SegmentObject { Id = first.Id };
            service.Add(first);

            Throws<InvalidOperationException>(() => service.Add(duplicate));
            Equal(1, scene.Objects.Count);
        }

        private static void InvalidCircleDoesNotChangeScene()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);

            Throws<ArgumentOutOfRangeException>(() => service.Add(new CircleObject { Radius = 0 }));
            Equal(0, scene.Objects.Count);
        }

        private static void RemoveUnknownObjectDoesNotChangeScene()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            True(!service.Remove(Guid.NewGuid()));
            Equal(0, scene.Objects.Count);
        }

        private static void SceneRoundTripsAllBaseObjects()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            service.Add(new PointObject { Position = new MathPoint(1, 2) });
            service.Add(new SegmentObject
            {
                Start = new MathPoint(3, 4),
                End = new MathPoint(5, 6)
            });
            service.Add(new CircleObject
            {
                Center = new MathPoint(7, 8),
                Radius = 9
            });
            service.Add(new TextLabelObject
            {
                Position = new MathPoint(10, 11),
                Text = "A"
            });
            service.Add(new LineObject
            {
                Start = new MathPoint(0, 0),
                End = new MathPoint(10, 10)
            });
            service.Add(new RayObject
            {
                Start = new MathPoint(2, 2),
                Through = new MathPoint(8, 2)
            });
            service.Add(new AngleMeasurementObject
            {
                Vertex = new MathPoint(0, 0),
                First = new MathPoint(1, 0),
                Second = new MathPoint(0, 1)
            });

            var result = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(scene));

            Equal(0, result.Issues.Count);
            Equal(7, result.Scene.Objects.Count);
            Equal(MathObjectType.Point, result.Scene.Objects[0].Type);
            Equal(9d, ((CircleObject)result.Scene.Objects[2]).Radius);
            Equal("A", ((TextLabelObject)result.Scene.Objects[3]).Text);
            Equal(MathObjectType.Line, result.Scene.Objects[4].Type);
            Equal(MathObjectType.Ray, result.Scene.Objects[5].Type);
            Equal(MathObjectType.AngleMeasurement, result.Scene.Objects[6].Type);
        }

        private static void UnknownObjectIsIsolated()
        {
            const string json = @"
                {
                  ""schemaVersion"": 1,
                  ""objects"": [
                    { ""type"": 99 },
                    {
                      ""id"": ""4d82199d-2c86-42c3-83bd-7203f945371a"",
                      ""objectVersion"": 1,
                      ""type"": 0,
                      ""source"": 0,
                      ""isVisible"": true,
                      ""strokeColor"": ""#FF000000"",
                      ""strokeWidth"": 2,
                      ""position"": { ""x"": 1, ""y"": 2 }
                    }
                  ]
                }";

            var result = MathSceneSerializer.Deserialize(json);

            Equal(1, result.Scene.Objects.Count);
            Equal(1, result.Issues.Count);
        }

        private static void InvalidObjectIsIsolated()
        {
            const string json = @"
                {
                  ""schemaVersion"": 1,
                  ""objects"": [
                    {
                      ""id"": ""5f21f519-7480-4c80-9949-e5a879d32bb7"",
                      ""objectVersion"": 1,
                      ""type"": 2,
                      ""source"": 0,
                      ""isVisible"": true,
                      ""strokeColor"": ""#FF000000"",
                      ""strokeWidth"": 2,
                      ""center"": { ""x"": 1, ""y"": 2 },
                      ""radius"": 0
                    },
                    {
                      ""id"": ""0e608e67-1449-4782-92fb-ae6e90dfb644"",
                      ""objectVersion"": 1,
                      ""type"": 3,
                      ""source"": 0,
                      ""isVisible"": true,
                      ""strokeColor"": ""#FF000000"",
                      ""strokeWidth"": 2,
                      ""position"": { ""x"": 3, ""y"": 4 },
                      ""text"": ""valid""
                    }
                  ]
                }";

            var result = MathSceneSerializer.Deserialize(json);

            Equal(1, result.Scene.Objects.Count);
            Equal(MathObjectType.TextLabel, result.Scene.Objects[0].Type);
            Equal(1, result.Issues.Count);
        }

        private static void MalformedSceneReturnsEmptyResult()
        {
            var result = MathSceneSerializer.Deserialize("{");

            Equal(0, result.Scene.Objects.Count);
            Equal(1, result.Issues.Count);
        }

        private static void HitTestPrefersTopmostObject()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var point = new PointObject { Position = new MathPoint(5, 5) };
            var segment = new SegmentObject
            {
                Start = new MathPoint(0, 5),
                End = new MathPoint(10, 5)
            };
            service.Add(point);
            service.Add(segment);

            Same(segment, MathGeometryService.HitTest(scene, new MathPoint(5, 5), 2));
        }

        private static void TranslateMovesSegmentEndpoints()
        {
            var segment = new SegmentObject
            {
                Start = new MathPoint(1, 2),
                End = new MathPoint(3, 4)
            };

            MathGeometryService.Translate(segment, 10, -2);

            Equal(11d, segment.Start.X);
            Equal(0d, segment.Start.Y);
            Equal(13d, segment.End.X);
            Equal(2d, segment.End.Y);
        }

        private static void SceneFileStoreRoundTripsAtomically()
        {
            var directory = Path.Combine(Path.GetTempPath(), "InkCanvasMathTests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "scene.math.json");
            try
            {
                var scene = new MathScene();
                new MathSceneService(scene).Add(new CircleObject
                {
                    Center = new MathPoint(20, 30),
                    Radius = 15
                });

                MathSceneFileStore.Save(path, scene);
                var result = MathSceneFileStore.Load(path);

                Equal(0, result.Issues.Count);
                Equal(1, result.Scene.Objects.Count);
                Equal(15d, ((CircleObject)result.Scene.Objects[0]).Radius);
                True(!File.Exists(path + ".tmp"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void LineIntersectionRespectsSegmentBounds()
        {
            var segment = new SegmentObject
            {
                Start = new MathPoint(0, 0),
                End = new MathPoint(2, 0)
            };
            var outside = new LineObject
            {
                Start = new MathPoint(3, -1),
                End = new MathPoint(3, 1)
            };
            var inside = new LineObject
            {
                Start = new MathPoint(1, -1),
                End = new MathPoint(1, 1)
            };

            Equal(0, MathIntersectionService.Intersect(segment, outside).Count);
            var intersections = MathIntersectionService.Intersect(segment, inside);
            Equal(1, intersections.Count);
            Equal(1d, intersections[0].X);
            Equal(0d, intersections[0].Y);
        }

        private static void LineCircleIntersectionReturnsTwoPoints()
        {
            var line = new LineObject
            {
                Start = new MathPoint(-10, 0),
                End = new MathPoint(10, 0)
            };
            var circle = new CircleObject
            {
                Center = new MathPoint(0, 0),
                Radius = 5
            };

            var intersections = MathIntersectionService.Intersect(line, circle);

            Equal(2, intersections.Count);
            Equal(-5d, intersections[0].X);
            Equal(5d, intersections[1].X);
        }

        private static void TangentCirclesReturnOnePoint()
        {
            var first = new CircleObject { Center = new MathPoint(0, 0), Radius = 5 };
            var second = new CircleObject { Center = new MathPoint(10, 0), Radius = 5 };

            var intersections = MathIntersectionService.Intersect(first, second);

            Equal(1, intersections.Count);
            Equal(5d, intersections[0].X);
            Equal(0d, intersections[0].Y);
        }

        private static void AngleMeasurementReturnsRightAngle()
        {
            var angle = MathMeasurementService.AngleDegrees(
                new MathPoint(1, 0),
                new MathPoint(0, 0),
                new MathPoint(0, 1));

            Equal(90d, angle);
        }

        private static void SnapFindsSegmentMidpoint()
        {
            var scene = new MathScene();
            new MathSceneService(scene).Add(new SegmentObject
            {
                Start = new MathPoint(0, 0),
                End = new MathPoint(10, 0)
            });

            True(MathSnapService.TrySnap(scene, new MathPoint(5.4, 0.2), 1, out MathPoint snapped));
            Equal(5d, snapped.X);
            Equal(0d, snapped.Y);
        }

        private static void SnapFindsIntersectionPoint()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            service.Add(new LineObject
            {
                Start = new MathPoint(0, 0),
                End = new MathPoint(10, 10)
            });
            service.Add(new LineObject
            {
                Start = new MathPoint(0, 10),
                End = new MathPoint(10, 0)
            });

            True(MathSnapService.TrySnap(scene, new MathPoint(5.2, 4.8), 1, out MathPoint snapped));
            Equal(5d, snapped.X);
            Equal(5d, snapped.Y);
        }

        private static void SnapReturnsPointReference()
        {
            var scene = new MathScene();
            var point = new PointObject { Position = new MathPoint(10, 20) };
            new MathSceneService(scene).Add(point);

            True(MathSnapService.TrySnap(
                scene,
                new MathPoint(10.4, 20.2),
                1,
                out MathSnapResult snapped));

            Equal(point.Id, snapped.PointObjectId.Value);
            Equal(10d, snapped.Position.X);
            Equal(20d, snapped.Position.Y);
        }

        private static void SnapProjectsToEdgesLinesAndSolids()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            service.Add(new SegmentObject
            {
                Start = new MathPoint(0, 0),
                End = new MathPoint(100, 0)
            });
            True(MathSnapService.TrySnap(scene, new MathPoint(42, 3), 5, out MathPoint edge));
            Equal(42d, edge.X);
            Equal(0d, edge.Y);

            service.Add(new LineObject
            {
                Start = new MathPoint(200, 0),
                End = new MathPoint(200, 100)
            });
            True(MathSnapService.TrySnap(scene, new MathPoint(204, 175), 5, out MathPoint line));
            Equal(200d, line.X);
            Equal(175d, line.Y);

            var solid = new SolidObject
            {
                SolidType = SolidType.Cuboid,
                Center = new MathPoint(400, 300),
                Scale = 30
            };
            service.Add(solid);
            var projected = SolidProjectionService.Project(solid).Edges[0];
            var midpoint = new MathPoint(
                (projected.Start.X + projected.End.X) / 2,
                (projected.Start.Y + projected.End.Y) / 2);
            True(MathSnapService.TrySnap(
                scene,
                new MathPoint(midpoint.X, midpoint.Y + 3),
                5,
                out MathPoint solidEdge));
            True(MathMeasurementService.Distance(midpoint, solidEdge) < 0.001);
        }

        private static void SnapReturnsSolidAttachmentCoordinates()
        {
            var scene = new MathScene();
            var solid = new SolidObject
            {
                SolidType = SolidType.Cuboid,
                Center = new MathPoint(400, 300),
                Width = 4,
                Height = 3,
                Depth = 2,
                Scale = 30
            };
            new MathSceneService(scene).Add(solid);

            var modelVertex = SolidMeshBuilder.Build(solid).Vertices[0];
            var screenVertex = SolidProjectionService.ProjectModelPoint(solid, modelVertex);
            True(MathSnapService.TrySnap(scene, screenVertex, 1, out MathSnapResult snapped));

            Equal(solid.Id, snapped.SolidId.Value);
            Equal(modelVertex.X, snapped.SolidLocalPoint.Value.X);
            Equal(modelVertex.Y, snapped.SolidLocalPoint.Value.Y);
            Equal(modelVertex.Z, snapped.SolidLocalPoint.Value.Z);
        }

        private static void SolidAttachmentsFollowParentAndRoundTrip()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var solid = new SolidObject
            {
                SolidType = SolidType.Cuboid,
                Center = new MathPoint(320, 240),
                Width = 4,
                Height = 3,
                Depth = 2,
                Scale = 30
            };
            service.Add(solid);
            var vertices = SolidMeshBuilder.Build(solid).Vertices;
            var segment = new SegmentObject
            {
                Start = SolidProjectionService.ProjectModelPoint(solid, vertices[0]),
                End = SolidProjectionService.ProjectModelPoint(solid, vertices[1])
            };
            True(SolidAttachmentService.TryAttach(
                segment,
                new MathSnapResult(segment.Start, null, solid.Id, vertices[0]),
                new MathSnapResult(segment.End, null, solid.Id, vertices[1])));
            service.Add(segment);

            solid.Center = new MathPoint(400, 280);
            solid.RotationY = 40;
            solid.Scale = 60;
            MathReferenceService.Synchronize(scene);
            var expectedStart = SolidProjectionService.ProjectModelPoint(solid, vertices[0]);
            Equal(expectedStart.X, segment.Start.X);
            Equal(expectedStart.Y, segment.Start.Y);

            MathReferenceService.Translate(scene, segment, 15, -10);
            Equal(415d, solid.Center.X);
            Equal(270d, solid.Center.Y);

            var restored = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(scene));
            Equal(0, restored.Issues.Count);
            var restoredSegment = (SegmentObject)restored.Scene.Objects[1];
            True(restoredSegment.SolidAttachment != null);
            Equal(vertices[0].X, restoredSegment.SolidAttachment.LocalPoints[0].X);
        }

        private static void StrictSolidSphereConstructionsRespectGeometry()
        {
            var cube = new SolidObject
            {
                SolidType = SolidType.Cube,
                Width = 4,
                Height = 4,
                Depth = 4
            };
            True(SolidSphereConstructionService.TryCreateCircumsphere(cube, out var cubeOuter));
            True(SolidSphereConstructionService.TryCreateInsphere(cube, out var cubeInner));
            Near(System.Math.Sqrt(12), cubeOuter.Radius);
            Equal(2d, cubeInner.Radius);

            var cuboid = new SolidObject
            {
                SolidType = SolidType.Cuboid,
                Width = 4,
                Height = 5,
                Depth = 6
            };
            True(!SolidSphereConstructionService.TryCreateInsphere(cuboid, out _));

            var cylinder = new SolidObject
            {
                SolidType = SolidType.Cylinder,
                Radius = 2,
                Height = 4
            };
            True(SolidSphereConstructionService.TryCreateInsphere(cylinder, out var cylinderInner));
            Equal(2d, cylinderInner.Radius);
        }

        private static void TriangleCirclesFollowTheirTriangle()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var triangle = new TriangleObject
            {
                First = new MathPoint(0, 0),
                Second = new MathPoint(4, 0),
                Third = new MathPoint(0, 3)
            };
            service.Add(triangle);
            True(TriangleCircleConstructionService.TryCreate(
                triangle,
                TriangleCircleKind.Circumcircle,
                out var circumcircle));
            True(TriangleCircleConstructionService.TryCreate(
                triangle,
                TriangleCircleKind.Incircle,
                out var incircle));
            service.Add(circumcircle);
            service.Add(incircle);
            Equal(2d, circumcircle.Center.X);
            Equal(1.5d, circumcircle.Center.Y);
            Equal(2.5d, circumcircle.Radius);
            Equal(1d, incircle.Center.X);
            Equal(1d, incircle.Center.Y);
            Equal(1d, incircle.Radius);

            MathReferenceService.Translate(scene, triangle, 10, -5);
            Equal(12d, circumcircle.Center.X);
            Equal(-3.5d, circumcircle.Center.Y);
            Equal(11d, incircle.Center.X);
            Equal(-4d, incircle.Center.Y);

            var restored = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(scene));
            Equal(0, restored.Issues.Count);
            Equal(3, restored.Scene.Objects.Count);
            var restoredCircle = (CircleObject)restored.Scene.Objects[1];
            Equal(triangle.Id, restoredCircle.TriangleId.Value);
            Equal(TriangleCircleKind.Circumcircle, restoredCircle.TriangleCircleKind.Value);
        }

        private static void MovingPointUpdatesReferencedSegment()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var point = new PointObject { Position = new MathPoint(1, 2) };
            service.Add(point);
            var segment = new SegmentObject
            {
                Start = point.Position,
                End = new MathPoint(8, 9),
                StartPointId = point.Id
            };
            service.Add(segment);

            MathReferenceService.Translate(scene, point, 4, -1);

            Equal(5d, point.Position.X);
            Equal(1d, point.Position.Y);
            Equal(5d, segment.Start.X);
            Equal(1d, segment.Start.Y);
        }

        private static void RemovingPointDetachesReferencedSegment()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var point = new PointObject { Position = new MathPoint(2, 3) };
            service.Add(point);
            var segment = new SegmentObject
            {
                Start = point.Position,
                End = new MathPoint(7, 8),
                StartPointId = point.Id
            };
            service.Add(segment);

            True(service.Remove(point.Id));

            Equal(null, segment.StartPointId);
            Equal(2d, segment.Start.X);
            Equal(3d, segment.Start.Y);
        }

        private static void PointReferencesRoundTrip()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var point = new PointObject { Position = new MathPoint(3, 4) };
            service.Add(point);
            service.Add(new SegmentObject
            {
                Start = point.Position,
                End = new MathPoint(9, 9),
                StartPointId = point.Id
            });

            var result = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(scene));
            var segment = (SegmentObject)result.Scene.Objects[1];

            Equal(point.Id, segment.StartPointId.Value);
            Equal(3d, segment.Start.X);
            Equal(4d, segment.Start.Y);
        }

        private static void HorizontalConstraintUpdatesSegment()
        {
            var scene = new MathScene();
            var segment = new SegmentObject
            {
                Start = new MathPoint(0, 2),
                End = new MathPoint(10, 8)
            };
            new MathSceneService(scene).Add(segment);
            MathConstraintService.Add(scene, new MathConstraint
            {
                Type = MathConstraintType.Horizontal,
                ObjectIds = { segment.Id }
            });

            True(MathConstraintService.TryApplyAll(scene, out var error));
            Equal(null, error);
            Equal(2d, segment.End.Y);
        }

        private static void ConflictingConstraintsRestoreScene()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var point = new PointObject { Position = new MathPoint(2, 2) };
            var line = new LineObject
            {
                Start = new MathPoint(-10, 0),
                End = new MathPoint(10, 0)
            };
            var circle = new CircleObject
            {
                Center = new MathPoint(0, 10),
                Radius = 1
            };
            service.Add(point);
            service.Add(line);
            service.Add(circle);
            MathConstraintService.Add(scene, new MathConstraint
            {
                Type = MathConstraintType.PointOnLine,
                ObjectIds = { point.Id, line.Id }
            });
            MathConstraintService.Add(scene, new MathConstraint
            {
                Type = MathConstraintType.PointOnCircle,
                ObjectIds = { point.Id, circle.Id }
            });

            True(!MathConstraintService.TryApplyAll(scene, out var error));
            True(!string.IsNullOrWhiteSpace(error));
            Equal(2d, point.Position.X);
            Equal(2d, point.Position.Y);
        }

        private static void ConstraintsRoundTrip()
        {
            var scene = new MathScene();
            var segment = new SegmentObject
            {
                Start = new MathPoint(0, 0),
                End = new MathPoint(4, 2)
            };
            new MathSceneService(scene).Add(segment);
            MathConstraintService.Add(scene, new MathConstraint
            {
                Type = MathConstraintType.Horizontal,
                ObjectIds = { segment.Id }
            });

            var result = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(scene));

            Equal(0, result.Issues.Count);
            Equal(1, result.Scene.Constraints.Count);
            Equal(MathConstraintType.Horizontal, result.Scene.Constraints[0].Type);
            Equal(segment.Id, result.Scene.Constraints[0].ObjectIds[0]);
        }

        private static void ParallelAndPerpendicularConstraintsRoundTrip()
        {
            var parallelScene = new MathScene();
            var parallelService = new MathSceneService(parallelScene);
            var reference = new SegmentObject
            {
                Start = new MathPoint(0, 0),
                End = new MathPoint(4, 0)
            };
            var parallelTarget = new SegmentObject
            {
                Start = new MathPoint(1, 1),
                End = new MathPoint(2, 4)
            };
            parallelService.Add(reference);
            parallelService.Add(parallelTarget);
            var parallel = new MathConstraint
            {
                Type = MathConstraintType.Parallel,
                ObjectIds = { reference.Id, parallelTarget.Id }
            };
            MathConstraintService.Add(parallelScene, parallel);
            True(MathConstraintService.TryApplyAll(parallelScene, out _));
            True(MathConstraintService.IsSatisfied(parallelScene, parallel, 0.001));

            var perpendicularScene = new MathScene();
            var perpendicularService = new MathSceneService(perpendicularScene);
            var perpendicularReference = new SegmentObject
            {
                Start = new MathPoint(0, 0),
                End = new MathPoint(4, 0)
            };
            var perpendicularTarget = new SegmentObject
            {
                Start = new MathPoint(1, 1),
                End = new MathPoint(3, 3)
            };
            perpendicularService.Add(perpendicularReference);
            perpendicularService.Add(perpendicularTarget);
            var perpendicular = new MathConstraint
            {
                Type = MathConstraintType.Perpendicular,
                ObjectIds = { perpendicularReference.Id, perpendicularTarget.Id }
            };
            MathConstraintService.Add(perpendicularScene, perpendicular);
            True(MathConstraintService.TryApplyAll(perpendicularScene, out _));
            True(MathConstraintService.IsSatisfied(perpendicularScene, perpendicular, 0.001));

            var restored = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(perpendicularScene));
            Equal(0, restored.Issues.Count);
            Equal(MathConstraintType.Perpendicular, restored.Scene.Constraints[0].Type);
        }

        private static void ThreeHundredObjectInteractionCompletesWithinBudget()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            for (var i = 0; i < 150; i++)
            {
                service.Add(new PointObject
                {
                    Position = new MathPoint(i * 4, i % 12 * 10)
                });
                service.Add(new SegmentObject
                {
                    Start = new MathPoint(i * 4, 0),
                    End = new MathPoint(i * 4 + 20, 120)
                });
            }

            var stopwatch = Stopwatch.StartNew();
            var hit = MathGeometryService.HitTest(scene, new MathPoint(300, 60), 12);
            MathSnapService.TrySnap(scene, new MathPoint(301, 61), 12, out MathPoint _);
            var json = MathSceneSerializer.Serialize(scene);
            var restored = MathSceneSerializer.Deserialize(json);
            stopwatch.Stop();

            True(hit != null);
            Equal(300, restored.Scene.Objects.Count);
            True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
            Console.WriteLine($"METRIC 300-object interaction: {stopwatch.Elapsed.TotalMilliseconds:0.###} ms");
        }

        private static void ExpressionParserSupportsRequiredOperations()
        {
            var expression = MathExpressionParser.Parse(
                "y=sin(x)+cos(x)+tan(0)+sqrt(4)+abs(-3)+log(e)+exp(0)+2^3/2");

            var actual = expression.Evaluate(0);

            Equal(12d, System.Math.Round(actual, 10));
            Equal(-4d, MathExpressionParser.Parse("-2^2").Evaluate(0));
        }

        private static void ExpressionParserRejectsUnsafeInput()
        {
            Throws<FormatException>(() => MathExpressionParser.Parse("System.IO.File.Delete(x)"));
            Throws<FormatException>(() => MathExpressionParser.Parse("x;1"));
            Throws<FormatException>(() => MathExpressionParser.Parse(new string('x', 257)));
        }

        private static void FunctionSamplerDrawsRequiredExamples()
        {
            var quadratic = FunctionSamplingService.Sample(new FunctionObject
            {
                Expression = "x^2-4",
                DomainMin = -5,
                DomainMax = 5
            });
            var sine = FunctionSamplingService.Sample(new FunctionObject
            {
                Expression = "sin(x)",
                DomainMin = -System.Math.PI,
                DomainMax = System.Math.PI
            });

            True(quadratic.Segments.Count > 0);
            True(quadratic.Zeros.Count >= 2);
            True(quadratic.Extrema.Count >= 1);
            True(sine.Segments.Count > 0);
            True(sine.Zeros.Count >= 1);
        }

        private static void FunctionAnalysisReturnsCoordinateProperties()
        {
            var analysis = FunctionAnalysisService.Analyze(new FunctionObject
            {
                Expression = "x^2",
                DomainMin = -2,
                DomainMax = 2,
                SampleQuality = 3
            });

            True(analysis.YAxisIntercept.HasValue);
            Equal(0d, analysis.YAxisIntercept.Value.X);
            Equal(0d, analysis.YAxisIntercept.Value.Y);
            True(analysis.Zeros.Exists(point => System.Math.Abs(point.X) < 0.001));
            True(analysis.Extrema.Exists(point => System.Math.Abs(point.X) < 0.001));
            True(analysis.MonotonicIntervals.Exists(interval =>
                interval.Monotonicity == FunctionMonotonicity.Decreasing && interval.End <= 0.001));
            True(analysis.MonotonicIntervals.Exists(interval =>
                interval.Monotonicity == FunctionMonotonicity.Increasing && interval.Start >= -0.001));
        }

        private static void FunctionSamplerSplitsDiscontinuity()
        {
            var reciprocal = FunctionSamplingService.Sample(new FunctionObject
            {
                Expression = "1/x",
                DomainMin = -5,
                DomainMax = 5,
                SampleQuality = 3
            });

            True(reciprocal.Segments.Count >= 2);
            for (var i = 0; i < reciprocal.Segments.Count; i++)
            {
                var segment = reciprocal.Segments[i];
                True(!(segment[0].X < 0 && segment[segment.Count - 1].X > 0));
            }
        }

        private static void FunctionRoundTripsEditableExpression()
        {
            var scene = new MathScene();
            new MathSceneService(scene).Add(new FunctionObject
            {
                Expression = "sin(x)+1",
                DomainMin = -8,
                DomainMax = 12,
                Origin = new MathPoint(400, 300),
                RotationDegrees = 30
            });

            var result = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(scene));
            var function = (FunctionObject)result.Scene.Objects[0];

            Equal(0, result.Issues.Count);
            Equal("sin(x)+1", function.Expression);
            Equal(-8d, function.DomainMin);
            Equal(12d, function.DomainMax);
            Equal(30d, function.RotationDegrees);
        }

        private static void MovingFunctionMovesItsCoordinateFrame()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var plane = new CoordinatePlaneObject { Center = new MathPoint(300, 200) };
            var first = new FunctionObject
            {
                Origin = plane.Center,
                CoordinatePlaneId = plane.Id
            };
            var second = new FunctionObject
            {
                Expression = "x^2",
                Origin = plane.Center,
                CoordinatePlaneId = plane.Id
            };
            service.Add(plane);
            service.Add(first);
            service.Add(second);

            MathReferenceService.Translate(scene, first, 40, -25);

            Equal(new MathPoint(340, 175), plane.Center);
            Equal(plane.Center, first.Origin);
            Equal(plane.Center, second.Origin);
            var restored = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(scene)).Scene;
            Equal(plane.Id, ((FunctionObject)restored.Objects[1]).CoordinatePlaneId.Value);
        }

        private static void MovingLegacyFunctionFindsContainingCoordinateFrame()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var plane = new CoordinatePlaneObject { Center = new MathPoint(500, 300) };
            var function = new FunctionObject { Origin = plane.Center };
            service.Add(plane);
            service.Add(function);

            MathReferenceService.Translate(scene, function, -20, 30);

            Equal(plane.Id, function.CoordinatePlaneId.Value);
            Equal(new MathPoint(480, 330), plane.Center);
            Equal(plane.Center, function.Origin);
        }

        private static void RotatedFunctionCanBeSelected()
        {
            var scene = new MathScene();
            var function = new FunctionObject
            {
                Expression = "0",
                DomainMin = -2,
                DomainMax = 2,
                Origin = new MathPoint(200, 200),
                PixelsPerUnit = 40,
                RotationDegrees = 90,
                ShowZeros = false,
                ShowExtrema = false,
                ShowIntersections = false
            };
            new MathSceneService(scene).Add(function);

            Same(function, MathGeometryService.HitTest(scene, new MathPoint(200, 250), 4));
        }

        private static void ContrastStrokeColorAdaptsToBackground()
        {
            Equal(
                MathAppearanceService.LightStrokeColor,
                MathAppearanceService.GetContrastingStrokeColor(22, 41, 36));
            Equal(
                MathAppearanceService.DarkStrokeColor,
                MathAppearanceService.GetContrastingStrokeColor(255, 255, 255));
        }

        private static void TwentyFunctionCurvesCompleteWithinBudget()
        {
            var stopwatch = Stopwatch.StartNew();
            var pointCount = 0;
            for (var i = 0; i < 20; i++)
            {
                var sample = FunctionSamplingService.Sample(new FunctionObject
                {
                    Expression = i % 3 == 0 ? "sin(x)" : i % 3 == 1 ? "x^2-4" : "1/x",
                    DomainMin = -10,
                    DomainMax = 10,
                    SampleQuality = 2
                });
                for (var segmentIndex = 0; segmentIndex < sample.Segments.Count; segmentIndex++)
                    pointCount += sample.Segments[segmentIndex].Count;
            }
            stopwatch.Stop();

            True(pointCount > 0);
            True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
            Console.WriteLine($"METRIC 20-function sampling: {stopwatch.Elapsed.TotalMilliseconds:0.###} ms, {pointCount} points");
        }

        private static void FunctionIntersectionsAreDetected()
        {
            var first = new FunctionObject
            {
                Expression = "x",
                DomainMin = -5,
                DomainMax = 5
            };
            var second = new FunctionObject
            {
                Expression = "-x",
                DomainMin = -5,
                DomainMax = 5
            };

            var intersections = FunctionAnalysisService.FindIntersections(first, second);

            Equal(1, intersections.Count);
            True(System.Math.Abs(intersections[0].X) < 0.001);
            True(System.Math.Abs(intersections[0].Y) < 0.001);
        }

        private static void FunctionIntersectionsRequireSharedCoordinateFrame()
        {
            var first = new FunctionObject
            {
                Expression = "x",
                Origin = new MathPoint(300, 200),
                PixelsPerUnit = 40
            };
            var second = new FunctionObject
            {
                Expression = "-x",
                Origin = new MathPoint(300, 200),
                PixelsPerUnit = 40
            };
            True(FunctionAnalysisService.ShareCoordinateFrame(first, second));
            second.Origin = new MathPoint(320, 200);
            True(!FunctionAnalysisService.ShareCoordinateFrame(first, second));
        }

        private static void FunctionIntersectionsAreCachedUntilInputsChange()
        {
            var first = new FunctionObject { Expression = "x^2", DomainMin = -3, DomainMax = 3 };
            var second = new FunctionObject { Expression = "1", DomainMin = -3, DomainMax = 3 };
            var initial = FunctionAnalysisService.FindIntersections(first, second);
            var cached = FunctionAnalysisService.FindIntersections(first, second);
            Same(initial, cached);
            second.Expression = "2";
            var changed = FunctionAnalysisService.FindIntersections(first, second);
            True(!ReferenceEquals(initial, changed));
        }

        private static void FunctionSamplesAreReusedUntilInputsChange()
        {
            var function = new FunctionObject
            {
                Expression = "sin(x)",
                DomainMin = -10,
                DomainMax = 10,
                SampleQuality = 2
            };

            var first = FunctionSamplingService.Sample(function);
            var second = FunctionSamplingService.Sample(function);
            Same(first, second);

            function.DomainMax = 12;
            var changed = FunctionSamplingService.Sample(function);
            True(!ReferenceEquals(first, changed));
        }

        private static void CachedFunctionSamplingAvoidsRepeatedWork()
        {
            var function = new FunctionObject
            {
                Expression = "sin(x)+x^2/10",
                DomainMin = -10,
                DomainMax = 10,
                SampleQuality = 3
            };
            FunctionSamplingService.Sample(function);

            const int WarmIterations = 10000;
            var warm = Stopwatch.StartNew();
            for (var i = 0; i < WarmIterations; i++)
                FunctionSamplingService.Sample(function);
            warm.Stop();

            const int ColdIterations = 200;
            var cold = Stopwatch.StartNew();
            for (var i = 0; i < ColdIterations; i++)
            {
                FunctionSamplingService.Sample(new FunctionObject
                {
                    Expression = "sin(x)+x^2/10",
                    DomainMin = -10,
                    DomainMax = 10,
                    SampleQuality = 3
                });
            }
            cold.Stop();

            var warmMicroseconds = warm.Elapsed.TotalMilliseconds * 1000 / WarmIterations;
            var coldMicroseconds = cold.Elapsed.TotalMilliseconds * 1000 / ColdIterations;
            True(warmMicroseconds < coldMicroseconds);
            Console.WriteLine(
                $"METRIC function sampling per call: cached {warmMicroseconds:0.###} µs, cold {coldMicroseconds:0.###} µs");
        }

        private static void CoordinatePlaneClickUsesDefaultSizeAndDragUsesCustomSize()
        {
            var clicked = MathPlacementService.CreateCoordinatePlane(
                new MathPoint(320, 240),
                new MathPoint(320, 240),
                40,
                true,
                true);
            Equal(320d, clicked.Center.X);
            Equal(240d, clicked.Center.Y);
            Equal(MathPlacementService.DefaultCoordinatePlaneWidth, clicked.Width);
            Equal(MathPlacementService.DefaultCoordinatePlaneHeight, clicked.Height);

            var dragged = MathPlacementService.CreateCoordinatePlane(
                new MathPoint(100, 120),
                new MathPoint(700, 520),
                32,
                true,
                false);
            Equal(400d, dragged.Center.X);
            Equal(320d, dragged.Center.Y);
            Equal(600d, dragged.Width);
            Equal(400d, dragged.Height);
            Equal(32d, dragged.GridSpacing);
            True(dragged.ShowGrid);
            True(!dragged.ShowAxes);
        }

        private static void CoordinatePlaneCanBeInsertedMovedResizedAndSaved()
        {
            var scene = new MathScene();
            var coordinatePlane = new CoordinatePlaneObject
            {
                Center = new MathPoint(300, 220),
                Width = 480,
                Height = 320,
                GridSpacing = 32
            };
            new MathSceneService(scene).Add(coordinatePlane);

            Equal(coordinatePlane, MathGeometryService.HitTest(scene, new MathPoint(300, 220), 12));
            MathGeometryService.Translate(coordinatePlane, 25, -10);
            coordinatePlane.Width *= 1.25;
            coordinatePlane.Height *= 1.25;
            coordinatePlane.GridSpacing *= 1.25;

            var result = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(scene));
            var restored = (CoordinatePlaneObject)result.Scene.Objects[0];
            Equal(0, result.Issues.Count);
            Equal(325d, restored.Center.X);
            Equal(210d, restored.Center.Y);
            Equal(600d, restored.Width);
            Equal(400d, restored.Height);
            Equal(40d, restored.GridSpacing);
        }

        private static void CoordinatePlaneStaysBehindLaterMathObjects()
        {
            var scene = new MathScene();
            var service = new MathSceneService(scene);
            var segment = new SegmentObject
            {
                Start = new MathPoint(200, 220),
                End = new MathPoint(400, 220)
            };
            service.Add(segment);
            service.Add(new CoordinatePlaneObject
            {
                Center = new MathPoint(300, 220),
                Width = 480,
                Height = 320
            });

            True(scene.Objects[0] is CoordinatePlaneObject);
            Equal(segment, MathGeometryService.HitTest(scene, new MathPoint(300, 220), 12));
        }

        private static void AllSolidTypesBuildAndProject()
        {
            foreach (SolidType type in Enum.GetValues(typeof(SolidType)))
            {
                var solid = new SolidObject { SolidType = type };
                var mesh = SolidMeshBuilder.Build(solid);
                var projection = SolidProjectionService.Project(solid);

                True(mesh.Vertices.Count > 0);
                True(mesh.Edges.Count > 0);
                Equal(mesh.Vertices.Count, projection.Points.Count);
                Equal(mesh.Edges.Count, projection.Edges.Count);
                True(SolidMeasurementService.Volume(solid) > 0);
                True(double.IsFinite(SolidMeasurementService.SurfaceArea(solid)));
                True(SolidMeasurementService.SurfaceArea(solid) > 0);
            }
        }

        private static void SolidDefaultsUseClassroomIsometricView()
        {
            var solid = new SolidObject();
            True(!solid.ShowAxes);
            True(solid.Scale >= 50);
            Equal(SolidProjectionMode.Orthographic, solid.ProjectionMode);
            True(System.Math.Abs(solid.RotationY) < 0.001);
            True(System.Math.Abs(solid.RotationX) < 0.001);
            True(System.Math.Abs(solid.RotationZ) < 0.001);
            Equal(1d, solid.HorizontalScale);
            Equal(1d, solid.VerticalScale);
        }

        private static void SolidAxesProjectToDistinctDirections()
        {
            var solid = new SolidObject { ProjectionMode = SolidProjectionMode.Orthographic };
            var origin = SolidProjectionService.ProjectModelPoint(solid, new MathPoint3D(0, 0, 0));
            var x = SolidProjectionService.ProjectModelPoint(solid, new MathPoint3D(2, 0, 0));
            var y = SolidProjectionService.ProjectModelPoint(solid, new MathPoint3D(0, 2, 0));
            var z = SolidProjectionService.ProjectModelPoint(solid, new MathPoint3D(0, 0, 2));
            True(MathMeasurementService.Distance(origin, x) > 20);
            True(MathMeasurementService.Distance(origin, y) > 20);
            True(MathMeasurementService.Distance(origin, z) > 20);
            True(MathMeasurementService.Distance(x, y) > 20);
            True(MathMeasurementService.Distance(x, z) > 20);
        }

        private static void SolidProjectionKeepsParallelEdgesParallel()
        {
            var solid = new SolidObject
            {
                SolidType = SolidType.Cuboid,
                Width = 5,
                Height = 3,
                Depth = 2
            };
            var projection = SolidProjectionService.Project(solid);
            var first = projection.Edges[0];
            var opposite = projection.Edges[2];
            var cross = (first.End.X - first.Start.X) * (opposite.End.Y - opposite.Start.Y) -
                        (first.End.Y - first.Start.Y) * (opposite.End.X - opposite.Start.X);
            True(System.Math.Abs(cross) < 0.001);
        }

        private static void FrontViewCollapsesDepthAxis()
        {
            var solid = new SolidObject
            {
                Center = new MathPoint(320, 240),
                Scale = 40,
                ViewMode = SolidViewMode.Front
            };

            var origin = SolidProjectionService.ProjectModelPoint(solid, new MathPoint3D(0, 0, 0));
            var width = SolidProjectionService.ProjectModelPoint(solid, new MathPoint3D(2, 0, 0));
            var height = SolidProjectionService.ProjectModelPoint(solid, new MathPoint3D(0, 2, 0));
            var depth = SolidProjectionService.ProjectModelPoint(solid, new MathPoint3D(0, 0, 2));

            Equal(origin.X, depth.X);
            Equal(origin.Y, depth.Y);
            True(System.Math.Abs(origin.X - width.X) > 20);
            True(System.Math.Abs(origin.Y - height.Y) > 20);
        }

        private static void SolidViewModeRoundTripsAndLegacyScenesDefaultToProjection()
        {
            var scene = new MathScene();
            new MathSceneService(scene).Add(new SolidObject { ViewMode = SolidViewMode.Front });

            var restored = (SolidObject)MathSceneSerializer
                .Deserialize(MathSceneSerializer.Serialize(scene)).Scene.Objects[0];
            Equal(SolidViewMode.Front, restored.ViewMode);

            const string legacyJson = @"{
                ""schemaVersion"": 3,
                ""objects"": [{
                    ""id"": ""30a1f83f-410a-4e29-9d33-99d50aab3e2c"",
                    ""objectVersion"": 1,
                    ""type"": 8,
                    ""source"": 0,
                    ""isVisible"": true,
                    ""strokeColor"": ""#FF000000"",
                    ""strokeWidth"": 2,
                    ""solidType"": 0,
                    ""center"": { ""x"": 0, ""y"": 0 },
                    ""width"": 3,
                    ""height"": 3,
                    ""depth"": 3,
                    ""radius"": 1.5,
                    ""scale"": 55,
                    ""horizontalScale"": 1,
                    ""verticalScale"": 1,
                    ""rotationX"": 0,
                    ""rotationY"": 0,
                    ""rotationZ"": 0,
                    ""projectionMode"": 0,
                    ""showHiddenEdges"": true,
                    ""showLabels"": true,
                    ""showAxes"": false,
                    ""renderQuality"": 2
                }],
                ""constraints"": []
            }";
            var legacy = MathSceneSerializer.Deserialize(legacyJson);

            Equal(0, legacy.Issues.Count);
            Equal(SolidViewMode.Projection, ((SolidObject)legacy.Scene.Objects[0]).ViewMode);
        }

        private static void CurvedSolidsUseOnlyKeyConstructionLines()
        {
            var cylinder = new SolidObject { SolidType = SolidType.Cylinder, RenderQuality = 2 };
            var cone = new SolidObject { SolidType = SolidType.Cone, RenderQuality = 2 };
            var sphere = new SolidObject { SolidType = SolidType.Sphere, RenderQuality = 2 };
            Equal(42, SolidMeshBuilder.Build(cylinder).Edges.Count);
            Equal(22, SolidMeshBuilder.Build(cone).Edges.Count);
            Equal(60, SolidMeshBuilder.Build(sphere).Edges.Count);
        }

        private static void SolidInteriorCanBeSelected()
        {
            var scene = new MathScene();
            var solid = new SolidObject
            {
                Center = new MathPoint(320, 240),
                SolidType = SolidType.Cuboid,
                Width = 5,
                Height = 3,
                Depth = 2
            };
            new MathSceneService(scene).Add(solid);

            Same(solid, MathGeometryService.HitTest(scene, solid.Center, 1));
        }

        private static void SolidCanStretchHorizontallyAndVertically()
        {
            var solid = new SolidObject();
            var before = SolidProjectionService.Project(solid);
            var beforeWidth = ProjectedWidth(before);
            var beforeHeight = ProjectedHeight(before);

            MathGeometryService.StretchSolid(solid, 1.5, 1);
            var horizontal = SolidProjectionService.Project(solid);
            True(ProjectedWidth(horizontal) > beforeWidth * 1.4);
            True(System.Math.Abs(ProjectedHeight(horizontal) - beforeHeight) < 0.001);

            MathGeometryService.StretchSolid(solid, 1, 1.4);
            var vertical = SolidProjectionService.Project(solid);
            True(ProjectedHeight(vertical) > beforeHeight * 1.3);

            var scene = new MathScene();
            new MathSceneService(scene).Add(solid);
            var restored = (SolidObject)MathSceneSerializer
                .Deserialize(MathSceneSerializer.Serialize(scene)).Scene.Objects[0];
            Equal(1.5d, restored.HorizontalScale);
            Equal(1.4d, restored.VerticalScale);
        }

        private static void SolidRotationSnapsToParallelAndPerpendicularViews()
        {
            Equal(0d, SolidRotationSnapService.SnapToRightAngle(-2));
            Equal(90d, SolidRotationSnapService.SnapToRightAngle(94));
            Equal(180d, SolidRotationSnapService.SnapToRightAngle(184));
            Equal(270d, SolidRotationSnapService.SnapToRightAngle(274));
            Equal(0d, SolidRotationSnapService.SnapToRightAngle(358));
            Equal(96d, SolidRotationSnapService.SnapToRightAngle(96));
        }

        private static double ProjectedWidth(SolidProjection projection)
        {
            return projection.Points.Max(point => point.X) -
                   projection.Points.Min(point => point.X);
        }

        private static double ProjectedHeight(SolidProjection projection)
        {
            return projection.Points.Max(point => point.Y) -
                   projection.Points.Min(point => point.Y);
        }

        private static void SolidRotationPreservesMeasurements()
        {
            var solid = new SolidObject
            {
                SolidType = SolidType.Cuboid,
                Width = 3,
                Height = 4,
                Depth = 5
            };
            var volume = SolidMeasurementService.Volume(solid);
            var area = SolidMeasurementService.SurfaceArea(solid);
            var before = SolidProjectionService.Project(solid);

            solid.RotationX += 47;
            solid.RotationY -= 81;
            solid.RotationZ += 19;
            var after = SolidProjectionService.Project(solid);

            Equal(volume, SolidMeasurementService.Volume(solid));
            Equal(area, SolidMeasurementService.SurfaceArea(solid));
            True(System.Math.Abs(before.Points[0].X - after.Points[0].X) > 0.001);
        }

        private static void SolidRoundTripsCameraAndDimensions()
        {
            var scene = new MathScene();
            new MathSceneService(scene).Add(new SolidObject
            {
                SolidType = SolidType.Cone,
                Center = new MathPoint(320, 240),
                Radius = 2.5,
                Height = 7,
                RotationX = 12,
                RotationY = 34,
                RotationZ = 56,
                Scale = 42,
                ShowAxes = false,
                ProjectionMode = SolidProjectionMode.Orthographic
            });

            var result = MathSceneSerializer.Deserialize(MathSceneSerializer.Serialize(scene));
            var solid = (SolidObject)result.Scene.Objects[0];

            Equal(0, result.Issues.Count);
            Equal(SolidType.Cone, solid.SolidType);
            Equal(2.5d, solid.Radius);
            Equal(7d, solid.Height);
            Equal(34d, solid.RotationY);
            Equal(42d, solid.Scale);
            True(!solid.ShowAxes);
            Equal(SolidProjectionMode.Orthographic, solid.ProjectionMode);
        }

        private static void VersionOneSceneRemainsReadable()
        {
            const string json = @"
                {
                  ""schemaVersion"": 1,
                  ""objects"": [
                    {
                      ""id"": ""30a1f83f-410a-4e29-9d33-99d50aab3e2c"",
                      ""objectVersion"": 1,
                      ""type"": 0,
                      ""source"": 0,
                      ""isVisible"": true,
                      ""strokeColor"": ""#FF000000"",
                      ""strokeWidth"": 2,
                      ""position"": { ""x"": 4, ""y"": 5 }
                    }
                  ]
                }";

            var result = MathSceneSerializer.Deserialize(json);

            Equal(0, result.Issues.Count);
            Equal(1, result.Scene.SchemaVersion);
            Equal(1, result.Scene.Objects.Count);
        }

        private static void VersionTwoSceneRemainsReadable()
        {
            var result = MathSceneSerializer.Deserialize(
                @"{ ""schemaVersion"": 2, ""objects"": [], ""constraints"": [] }");

            Equal(0, result.Issues.Count);
            Equal(2, result.Scene.SchemaVersion);
            Equal(0, result.Scene.Objects.Count);
        }

        private static void FutureSceneVersionIsRejected()
        {
            var result = MathSceneSerializer.Deserialize(
                @"{ ""schemaVersion"": 999, ""objects"": [] }");

            Equal(0, result.Scene.Objects.Count);
            Equal(1, result.Issues.Count);
        }

        private static void FullSceneFileRoundTripPreservesEveryLayer()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "InkCanvasMathIntegration",
                Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "full.math.json");
            try
            {
                var scene = new MathScene();
                var service = new MathSceneService(scene);
                var point = new PointObject { Position = new MathPoint(20, 30) };
                service.Add(point);
                var segment = new SegmentObject
                {
                    Start = point.Position,
                    End = new MathPoint(80, 30),
                    StartPointId = point.Id
                };
                service.Add(segment);
                service.Add(new FunctionObject
                {
                    Expression = "sin(x)",
                    Origin = new MathPoint(400, 300)
                });
                service.Add(new SolidObject
                {
                    SolidType = SolidType.Cylinder,
                    Center = new MathPoint(600, 320),
                    RotationY = 75
                });
                MathConstraintService.Add(scene, new MathConstraint
                {
                    Type = MathConstraintType.Horizontal,
                    ObjectIds = { segment.Id }
                });
                True(MathConstraintService.TryApplyAll(scene, out _));

                MathSceneFileStore.Save(path, scene);
                var result = MathSceneFileStore.Load(path);

                Equal(0, result.Issues.Count);
                Equal(MathScene.CurrentSchemaVersion, result.Scene.SchemaVersion);
                Equal(4, result.Scene.Objects.Count);
                Equal(1, result.Scene.Constraints.Count);
                Equal(point.Id, ((SegmentObject)result.Scene.Objects[1]).StartPointId.Value);
                Equal("sin(x)", ((FunctionObject)result.Scene.Objects[2]).Expression);
                Equal(SolidType.Cylinder, ((SolidObject)result.Scene.Objects[3]).SolidType);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void TimeMachineUndoRedoRestoresMathSnapshots()
        {
            var beforeScene = new MathScene();
            var afterScene = new MathScene();
            new MathSceneService(afterScene).Add(new PointObject
            {
                Position = new MathPoint(8, 9)
            });
            var beforeJson = MathSceneSerializer.Serialize(beforeScene);
            var afterJson = MathSceneSerializer.Serialize(afterScene);
            var timeMachine = new TimeMachine();
            timeMachine.CommitMathSceneHistory(beforeJson, afterJson);

            var undo = timeMachine.Undo();
            var undoJson = undo.StrokeHasBeenCleared
                ? undo.MathSceneBeforeJson
                : undo.MathSceneAfterJson;
            var undoneScene = MathSceneSerializer.Deserialize(undoJson).Scene;
            Equal(0, undoneScene.Objects.Count);

            var redo = timeMachine.Redo();
            var redoJson = redo.StrokeHasBeenCleared
                ? redo.MathSceneBeforeJson
                : redo.MathSceneAfterJson;
            var redoneScene = MathSceneSerializer.Deserialize(redoJson).Scene;
            Equal(1, redoneScene.Objects.Count);
        }

        private static void TimeMachineDropsRedoBranchAfterMathEdit()
        {
            var empty = MathSceneSerializer.Serialize(new MathScene());
            var firstScene = new MathScene();
            new MathSceneService(firstScene).Add(new PointObject
            {
                Position = new MathPoint(1, 1)
            });
            var first = MathSceneSerializer.Serialize(firstScene);
            var secondScene = new MathScene();
            new MathSceneService(secondScene).Add(new CircleObject
            {
                Center = new MathPoint(2, 2),
                Radius = 3
            });
            var second = MathSceneSerializer.Serialize(secondScene);
            var timeMachine = new TimeMachine();
            timeMachine.CommitMathSceneHistory(empty, first);
            timeMachine.Undo();

            timeMachine.CommitMathSceneHistory(empty, second);

            True(!timeMachine.CanRedo);
            var history = timeMachine.ExportTimeMachineHistory();
            Equal(1, history.Length);
            Equal(second, history[0].MathSceneAfterJson);
        }

        private static void GridCoordinatesUseCanvasCenterAndYAxisUp()
        {
            var origin = MathCoordinateService.ToGridCoordinate(
                new MathPoint(500, 300),
                1000,
                600,
                40);
            var upperRight = MathCoordinateService.ToGridCoordinate(
                new MathPoint(580, 220),
                1000,
                600,
                40);

            Equal(0d, origin.X);
            Equal(0d, origin.Y);
            Equal(2d, upperRight.X);
            Equal(2d, upperRight.Y);
            Equal(3d, MathCoordinateService.ToGridLength(120, 40));
        }

        private static void Run(string name, Action test)
        {
            test();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }

        private static void Same(object expected, object actual)
        {
            if (!ReferenceEquals(expected, actual))
                throw new InvalidOperationException("Expected references to be identical.");
        }

        private static void True(bool value)
        {
            if (!value) throw new InvalidOperationException("Expected true.");
        }

        private static void Near(double expected, double actual)
        {
            if (System.Math.Abs(expected - actual) > 1e-6)
                throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }

        private static void Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using QuizBattle.Arena;
using QuizBattle.UI.HUD;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuizBattle.Tests.EditMode
{
    public class ArenaResponsiveFramingTests
    {
        public static IEnumerable<TestCaseData> Viewports()
        {
            int[,] sizes = { { 1920, 1080 }, { 1900, 877 }, { 1366, 768 }, { 1024, 768 }, { 390, 844 }, { 844, 390 }, { 640, 360 }, { 568, 320 } };
            foreach (int rows in new[] { 6, 11, 31 })
                for (int i = 0; i < sizes.GetLength(0); i++)
                    foreach (bool inset in new[] { false, true })
                        yield return new TestCaseData(sizes[i, 0], sizes[i, 1], rows, inset);
        }

        [TestCaseSource(nameof(Viewports))]
        public void PlayableBoardAndTokenEnvelopeFitTheActualCameraViewport(int width, int height, int rows, bool inset)
        {
            var cameraObject = new GameObject("FramingCamera");
            var gridObject = new GameObject("FramingGrid");
            var target = new RenderTexture(width, height, 0);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                var pixels = HudController.GetCameraPixelRect(camera);
                var safe = inset ? new Rect(24f, 20f, width - 48f, height - 44f) : pixels;
                if (inset)
                {
                    gridObject.transform.position = new Vector3(4f, 7f, -3f);
                    gridObject.transform.rotation = Quaternion.Euler(12f, 31f, -7f);
                    gridObject.transform.localScale = new Vector3(1.2f, 0.9f, 1.1f);
                }
                var framer = cameraObject.AddComponent<ArenaCameraAutoFramer>();
                framer.SetGridTransform(gridObject.transform);
                framer.SetDimensions(8, rows);
                foreach (int state in new[] { 1, 3, 4, 6, 0 })
                {
                    var layout = HudController.CalculateLayout(pixels, safe, (state & 1) != 0, (state & 2) != 0, (state & 4) != 0);
                    var viewport = HudController.NormalizeViewport(layout.Board, pixels);
                    framer.ApplyFraming(viewport);
                    Assert.IsTrue(camera.orthographic);
                    Assert.Greater(viewport.width, 0f);
                    Assert.Greater(viewport.height, 0f);
                    Assert.That(viewport.xMin, Is.InRange(0f, 1f));
                    Assert.That(viewport.yMin, Is.InRange(0f, 1f));
                    Assert.That(viewport.xMax, Is.InRange(0f, 1f));
                    Assert.That(viewport.yMax, Is.InRange(0f, 1f));
                    var projectedMin = Vector2.one * float.PositiveInfinity;
                    var projectedMax = Vector2.one * float.NegativeInfinity;
                    for (int envelope = 0; envelope < 2; envelope++)
                    {
                        for (int corner = 0; corner < 8; corner++)
                        {
                            float x = (corner & 1) == 0 ? -0.66f : 7f * 1.32f + 0.66f;
                            float z = (corner & 4) == 0 ? -0.66f : (rows - 1) * 1.32f + 0.66f;
                            float y = 0f;
                            if (envelope == 1)
                            {
                                x += (corner & 1) == 0 ? -0.45f : 0.45f;
                                z += (corner & 4) == 0 ? -0.15f : 0.15f;
                                y = (corner & 2) == 0 ? -0.12f : 2.6f;
                            }
                            var point = camera.WorldToViewportPoint(gridObject.transform.TransformPoint(new Vector3(x, y, z)));
                            Assert.That(point.x, Is.InRange(viewport.xMin - 0.0001f, viewport.xMax + 0.0001f));
                            Assert.That(point.y, Is.InRange(viewport.yMin - 0.0001f, viewport.yMax + 0.0001f));
                            Assert.That(point.z, Is.InRange(camera.nearClipPlane, camera.farClipPlane));
                            if (envelope == 0)
                            {
                                projectedMin = Vector2.Min(projectedMin, point);
                                projectedMax = Vector2.Max(projectedMax, point);
                            }
                        }
                    }
                    float horizontal = (projectedMax.x - projectedMin.x) / viewport.width;
                    float vertical = (projectedMax.y - projectedMin.y) / viewport.height;
                    Assert.Greater(Mathf.Max(horizontal, vertical), 0.70f);
                    if ((state & 1) != 0)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var choice = layout.Choice(i);
                            Assert.GreaterOrEqual(choice.height, 48f);
                            Assert.GreaterOrEqual(choice.width, 140f);
                            Assert.IsFalse(choice.Overlaps(layout.Board));
                            Assert.IsFalse(choice.Overlaps(layout.Question));
                            Assert.GreaterOrEqual(choice.yMin, safe.yMin);
                            Assert.LessOrEqual(choice.xMax, safe.xMax);
                            for (int j = i + 1; j < 4; j++) Assert.IsFalse(choice.Overlaps(layout.Choice(j)));
                        }
                        if (!inset && (width >= 1366 || width == 844))
                        {
                            Assert.IsTrue(layout.SideHud);
                            Assert.That((layout.Board.xMin - safe.xMin) / safe.width, Is.InRange(0.30f, 0.34f));
                        }
                    }
                    if ((state & 2) != 0)
                    {
                        Assert.IsFalse(layout.Countdown.Overlaps(layout.Board));
                        Assert.GreaterOrEqual(layout.Countdown.yMin, safe.yMin);
                    }
                    if ((state & 4) != 0)
                    {
                        Assert.IsFalse(layout.Waiting.Overlaps(layout.Board));
                        Assert.IsFalse(layout.Waiting.Overlaps(layout.Countdown));
                        Assert.GreaterOrEqual(layout.Waiting.yMin, safe.yMin);
                    }
                }
                camera.rect = new Rect(0.2f, 0.1f, 0.6f, 0.7f);
                Assert.AreEqual(new Rect(0f, 0f, width, height), HudController.GetCameraPixelRect(camera));
                camera.targetTexture = null;
                camera.pixelRect = new Rect(80f, 40f, width * 0.7f, height * 0.8f);
                camera.ResetAspect();
                var subPixels = HudController.GetCameraPixelRect(camera);
                var subLayout = HudController.CalculateLayout(subPixels, new Rect(0f, 0f, width, height), true, false, false);
                var subViewport = HudController.NormalizeViewport(subLayout.Board, subPixels);
                framer.ApplyFraming(subViewport);
                var center = camera.WorldToViewportPoint(gridObject.transform.TransformPoint(new Vector3(4.62f, 1.24f, (rows - 1) * 0.66f)));
                Assert.That(center.x, Is.EqualTo(subViewport.center.x).Within(0.0001f));
                Assert.That(center.y, Is.EqualTo(subViewport.center.y).Within(0.0001f));
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3((corner & 1) == 0 ? -1.11f : 10.35f,
                                            (corner & 2) == 0 ? -0.12f : 2.6f,
                                            (corner & 4) == 0 ? -0.81f : (rows - 1) * 1.32f + 0.81f);
                    var screen = camera.WorldToScreenPoint(gridObject.transform.TransformPoint(local));
                    Assert.That(screen.x, Is.InRange(subLayout.Board.xMin - 0.01f, subLayout.Board.xMax + 0.01f));
                    Assert.That(screen.y, Is.InRange(subLayout.Board.yMin - 0.01f, subLayout.Board.yMax + 0.01f));
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(gridObject);
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1900, 877)]
        [TestCase(390, 844)]
        [TestCase(844, 390)]
        [TestCase(640, 360)]
        [TestCase(568, 320)]
        public void RenderTextureHudReservesItsActualLayoutAndKeepsAllQuizTextScrollable(int width, int height)
        {
            var previousEventSystem = Object.FindAnyObjectByType<EventSystem>();
            var cameraObject = new GameObject("HudScreenshotCamera");
            var target = new RenderTexture(width, height, 0);
            HudController hud = null;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var framer = cameraObject.AddComponent<ArenaCameraAutoFramer>();
                framer.SetDimensions(8, 11);
                hud = HudController.Create();
                var canvas = hud.GetComponent<Canvas>();
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                var pixels = HudController.GetCameraPixelRect(camera);
                var worldOnly = HudController.NormalizeViewport(HudController.CalculateLayout(pixels, pixels, false, false, false).Board, pixels);
                Assert.AreEqual(worldOnly, hud.GetBoardViewport(camera));

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1.5f;
                var question = string.Concat(System.Linq.Enumerable.Repeat("Read every condition before choosing the correct answer. ", 24)) + "QUESTION END";
                var answer = string.Concat(System.Linq.Enumerable.Repeat("This option includes another essential detail. ", 20)) + "ANSWER END";
                hud.ShowQuestion(7, question, new[] { answer, answer, answer, answer });
                framer.ApplyFraming();
                Canvas.ForceUpdateCanvases();
                var layout = HudController.CalculateLayout(pixels, pixels, true, false, false);
                var viewport = hud.GetBoardViewport(camera);
                Assert.AreEqual(HudController.NormalizeViewport(layout.Board, pixels), viewport);
                Assert.AreNotEqual(worldOnly, viewport);
                Assert.AreEqual(layout.Board, hud.BoardPixelRect);
                var center = camera.WorldToViewportPoint(new Vector3(4.62f, 1.24f, 6.6f));
                Assert.That(center.x, Is.EqualTo(viewport.center.x).Within(0.0001f));
                Assert.That(center.y, Is.EqualTo(viewport.center.y).Within(0.0001f));

                var corners = new Vector3[4];
                for (int i = -1; i < 4; i++)
                {
                    var panel = (RectTransform)hud.transform.Find(i < 0 ? "QuestionPlacard" : $"Choice_{i}");
                    var expected = i < 0 ? layout.Question : layout.Choice(i);
                    panel.GetWorldCorners(corners);
                    var lower = camera.WorldToScreenPoint(corners[0]);
                    var upper = camera.WorldToScreenPoint(corners[2]);
                    Assert.That(lower.x, Is.EqualTo(expected.xMin).Within(1f));
                    Assert.That(lower.y, Is.EqualTo(expected.yMin).Within(1f));
                    Assert.That(upper.x, Is.EqualTo(expected.xMax).Within(1f));
                    Assert.That(upper.y, Is.EqualTo(expected.yMax).Within(1f));
                    var scroll = panel.GetComponentInChildren<HudTextScroll>();
                    Assert.IsNotNull(scroll);
                    Assert.IsTrue(scroll.vertical);
                    Assert.IsFalse(scroll.horizontal);
                    Assert.IsNotNull(scroll.viewport.GetComponent<RectMask2D>());
                    Assert.AreEqual(ContentSizeFitter.FitMode.PreferredSize, scroll.content.GetComponent<ContentSizeFitter>().verticalFit);
                    var text = scroll.content.GetComponent("TextMeshProUGUI");
                    var type = text.GetType();
                    Assert.AreEqual(i < 0 ? question : answer, type.GetProperty("text").GetValue(text));
                    Assert.AreEqual("Overflow", type.GetProperty("overflowMode").GetValue(text).ToString());
                    Assert.AreEqual(true, type.GetProperty("enableWordWrapping").GetValue(text));
                    Assert.AreEqual(false, type.GetProperty("enableAutoSizing").GetValue(text));
                    Assert.GreaterOrEqual((float)type.GetProperty("fontSize").GetValue(text), 16f);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                    Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                    scroll.verticalNormalizedPosition = 0f;
                    scroll.content.GetWorldCorners(corners);
                    var bottom = scroll.viewport.InverseTransformPoint(corners[0]);
                    Assert.That(bottom.y, Is.EqualTo(scroll.viewport.rect.yMin).Within(0.1f));
                    Assert.IsNotNull(scroll.verticalScrollbar);
                }

                var button = hud.transform.Find("Choice_0").GetComponent<Button>();
                var answerScroll = button.GetComponentInChildren<HudTextScroll>();
                Assert.AreEqual(button.gameObject, ExecuteEvents.GetEventHandler<IDragHandler>(button.gameObject));
                Assert.AreEqual(button.gameObject, answerScroll.gameObject);
                int selections = 0;
                hud.ChoiceSelected += index => { Assert.AreEqual(0, index); selections++; };
                foreach (bool locked in new[] { false, true })
                {
                    hud.SetChoicesInteractable(!locked);
                    answerScroll.verticalNormalizedPosition = 1f;
                    var pointer = new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                        position = layout.Choice(0).center,
                        pressPosition = layout.Choice(0).center,
                        eligibleForClick = true,
                        pointerPress = button.gameObject,
                        pointerDrag = answerScroll.gameObject,
                        pointerPressRaycast = new RaycastResult
                        {
                            module = canvas.GetComponent<GraphicRaycaster>(),
                            gameObject = button.gameObject,
                            screenPosition = layout.Choice(0).center,
                        },
                    };
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(answerScroll.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
                    ExecuteEvents.Execute(answerScroll.gameObject, pointer, ExecuteEvents.beginDragHandler);
                    pointer.position += Vector2.up * 30f;
                    ExecuteEvents.Execute(answerScroll.gameObject, pointer, ExecuteEvents.dragHandler);
                    Assert.Greater(answerScroll.content.anchoredPosition.y, 0f);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                    if (pointer.eligibleForClick) ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    ExecuteEvents.Execute(answerScroll.gameObject, pointer, ExecuteEvents.endDragHandler);
                    Assert.IsFalse(pointer.eligibleForClick);
                    Assert.AreEqual(!locked, button.interactable);
                    Assert.AreEqual(locked ? 1 : 0, selections);
                    button.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                    Assert.AreEqual(1, selections);
                }
                hud.ShowQuestion(8, "Next question", new[] { "Next answer" });
                foreach (var scroll in hud.GetComponentsInChildren<HudTextScroll>())
                    Assert.AreEqual(Vector2.zero, scroll.content.anchoredPosition);
                Assert.IsTrue(button.interactable);
                Assert.IsFalse(hud.transform.Find("Choice_1").GetComponent<Button>().interactable);

                canvas.enabled = false;
                Assert.AreEqual(worldOnly, hud.GetBoardViewport(camera));
                canvas.enabled = true;
                canvas.worldCamera = null;
                Assert.AreEqual(worldOnly, hud.GetBoardViewport(camera));
            }
            finally
            {
                if (hud != null) Object.DestroyImmediate(hud.gameObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
                if (previousEventSystem == null && EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }
    }
}

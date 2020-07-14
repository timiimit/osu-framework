// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Framework.Tests.Visual.Input
{
    public class TestSceneTouchInput : ManualInputManagerTestScene
    {
        private static readonly TouchSource[] touch_sources = (TouchSource[])Enum.GetValues(typeof(TouchSource));

        private Container<InputReceptor> receptors;

        [SetUp]
        public new void SetUp() => Schedule(() =>
        {
            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Gray.Darken(2f),
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Text = "Parent"
                        },
                    }
                },
                receptors = new Container<InputReceptor>
                {
                    Padding = new MarginPadding { Bottom = 20f },
                    RelativeSizeAxes = Axes.Both,
                    ChildrenEnumerable = touch_sources.Select(s => new InputReceptor(s)
                    {
                        RelativePositionAxes = Axes.Both,
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Gray.Lighten((float)s / TouchState.MAX_TOUCH_COUNT),
                        X = (float)s / TouchState.MAX_TOUCH_COUNT,
                    })
                },
                new TestSceneTouchVisualiser.TouchVisualiser(),
            };
        });

        private float getTouchXPos(TouchSource source) => receptors[(int)source].DrawPosition.X + 10f;
        private Vector2 getTouchDownPos(TouchSource source) => receptors.ToScreenSpace(new Vector2(getTouchXPos(source), 1f));
        private Vector2 getTouchMovePos(TouchSource source) => receptors.ToScreenSpace(new Vector2(getTouchXPos(source), receptors.DrawHeight / 2f));
        private Vector2 getTouchUpPos(TouchSource source) => receptors.ToScreenSpace(new Vector2(getTouchXPos(source), receptors.DrawHeight - 1f));

        [Test]
        public void TestTouchInputHandling()
        {
            AddStep("activate touches", () =>
            {
                foreach (var s in touch_sources)
                    InputManager.BeginTouch(new Touch(s, getTouchDownPos(s)));
            });

            AddAssert("received correct event for each receptor", () =>
            {
                foreach (var r in receptors)
                {
                    // attempt dequeuing from touch events queue.
                    if (!(r.TouchEvents.TryDequeue(out TouchEvent te) && te is TouchDownEvent touchDown))
                        return false;

                    // check correct provided information.
                    if (touchDown.ScreenSpaceTouch.Source != r.AssociatedSource ||
                        touchDown.ScreenSpaceTouch.Position != getTouchDownPos(r.AssociatedSource) ||
                        touchDown.ScreenSpaceTouchDownPosition != getTouchDownPos(r.AssociatedSource))
                        return false;

                    // check no other events popped up.
                    if (r.TouchEvents.Count > 0)
                        return false;
                }

                return true;
            });

            AddStep("move touches", () =>
            {
                foreach (var s in touch_sources)
                    InputManager.MoveTouchTo(new Touch(s, getTouchMovePos(s)));
            });

            AddAssert("received correct event for each receptor", () =>
            {
                foreach (var r in receptors)
                {
                    if (!(r.TouchEvents.TryDequeue(out TouchEvent te) && te is TouchMoveEvent touchMove))
                        return false;

                    if (touchMove.ScreenSpaceTouch.Source != r.AssociatedSource ||
                        touchMove.ScreenSpaceTouch.Position != getTouchMovePos(r.AssociatedSource) ||
                        touchMove.ScreenSpaceLastTouchPosition != getTouchDownPos(r.AssociatedSource) ||
                        touchMove.ScreenSpaceTouchDownPosition != getTouchDownPos(r.AssociatedSource))
                        return false;

                    if (r.TouchEvents.Count > 0)
                        return false;
                }

                return true;
            });

            AddStep("move touches outside of area", () =>
            {
                foreach (var s in touch_sources)
                    InputManager.MoveTouchTo(new Touch(s, getTouchUpPos(s)));
            });

            AddAssert("received correct event for each receptor", () =>
            {
                foreach (var r in receptors)
                {
                    if (!(r.TouchEvents.TryDequeue(out TouchEvent te) && te is TouchMoveEvent touchMove))
                        return false;

                    if (touchMove.ScreenSpaceTouch.Source != r.AssociatedSource ||
                        touchMove.ScreenSpaceTouch.Position != getTouchUpPos(r.AssociatedSource) ||
                        touchMove.ScreenSpaceLastTouchPosition != getTouchMovePos(r.AssociatedSource) ||
                        touchMove.ScreenSpaceTouchDownPosition != getTouchDownPos(r.AssociatedSource))
                        return false;

                    if (r.TouchEvents.Count > 0)
                        return false;
                }

                return true;
            });

            AddStep("deactivate touches out of receptors", () =>
            {
                foreach (var s in touch_sources)
                    InputManager.EndTouch(new Touch(s, getTouchUpPos(s)));
            });

            AddAssert("received correct event for each receptor", () =>
            {
                foreach (var r in receptors)
                {
                    if (!(r.TouchEvents.TryDequeue(out TouchEvent te) && te is TouchUpEvent touchUp))
                        return false;

                    if (touchUp.ScreenSpaceTouch.Source != r.AssociatedSource ||
                        touchUp.ScreenSpaceTouch.Position != getTouchUpPos(r.AssociatedSource) ||
                        touchUp.ScreenSpaceTouchDownPosition != getTouchDownPos(r.AssociatedSource))
                        return false;

                    if (r.TouchEvents.Count > 0)
                        return false;
                }

                return true;
            });
        }

        [Test]
        public void TestMouseInputAppliedFromLatestTouch()
        {
            InputReceptor firstReceptor = null, lastReceptor = null;

            AddStep("retrieve receptors", () =>
            {
                firstReceptor = receptors[(int)TouchSource.Touch1];
                lastReceptor = receptors[(int)TouchSource.Touch10];
            });

            AddStep("activate first", () =>
            {
                InputManager.BeginTouch(new Touch(firstReceptor.AssociatedSource, getTouchDownPos(firstReceptor.AssociatedSource)));
            });

            AddAssert("received mouse-down event on first", () =>
            {
                // event #1: move mouse to first touch position.
                if (!(firstReceptor.MouseEvents.TryDequeue(out MouseEvent me1) && me1 is MouseMoveEvent mouseMove))
                    return false;

                if (mouseMove.ScreenSpaceMousePosition != getTouchDownPos(firstReceptor.AssociatedSource))
                    return false;

                // event #2: press mouse left-button (from first touch activation).
                if (!(firstReceptor.MouseEvents.TryDequeue(out MouseEvent me2) && me2 is MouseDownEvent mouseDown))
                    return false;

                if (mouseDown.Button != MouseButton.Left ||
                    mouseDown.ScreenSpaceMousePosition != getTouchDownPos(firstReceptor.AssociatedSource) ||
                    mouseDown.ScreenSpaceMouseDownPosition != getTouchDownPos(firstReceptor.AssociatedSource))
                    return false;

                return firstReceptor.MouseEvents.Count == 0;
            });

            // Activate each touch after first source and assert mouse has jumped to it.
            foreach (var s in touch_sources.Skip(1))
            {
                Touch touch = default;

                AddStep($"activate {s}", () => InputManager.BeginTouch(touch = new Touch(s, getTouchDownPos(s))));
                AddAssert("mouse jumped to new touch", () => assertMouseOnTouchChange(touch, null, true));
            }

            Vector2? lastMovePosition = null;

            // Move each touch inside area and assert regular mouse-move events received.
            foreach (var s in touch_sources)
            {
                Touch touch = default;

                AddStep($"move {s} inside area", () => InputManager.MoveTouchTo(touch = new Touch(s, getTouchMovePos(s))));
                AddAssert("received regular mouse-move event", () =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    var result = assertMouseOnTouchChange(touch, lastMovePosition, true);
                    lastMovePosition = touch.Position;
                    return result;
                });
            }

            // Move each touch outside of area and assert no MouseMoveEvent expected to be received.
            foreach (var s in touch_sources)
            {
                Touch touch = default;

                AddStep($"move {s} outside of area", () => InputManager.MoveTouchTo(touch = new Touch(s, getTouchUpPos(s))));
                AddAssert("no mouse-move event received", () =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    var result = assertMouseOnTouchChange(touch, lastMovePosition, false);
                    lastMovePosition = touch.Position;
                    return result;
                });
            }

            // Deactivate each touch but last touch and assert mouse did not jump to it.
            foreach (var s in touch_sources.SkipLast(1))
            {
                AddStep($"deactivate {s}", () => InputManager.EndTouch(new Touch(s, getTouchUpPos(s))));
                AddAssert("no mouse event received", () => receptors[(int)s].MouseEvents.Count == 0);
            }

            AddStep("deactivate last", () =>
            {
                InputManager.EndTouch(new Touch(lastReceptor.AssociatedSource, getTouchUpPos(lastReceptor.AssociatedSource)));
            });

            AddAssert("received mouse-up event", () =>
            {
                // First receptor is the one handling the mouse down event, mouse up would be raised to it.
                if (!(firstReceptor.MouseEvents.TryDequeue(out MouseEvent me) && me is MouseUpEvent mouseUp))
                    return false;

                if (mouseUp.Button != MouseButton.Left ||
                    mouseUp.ScreenSpaceMousePosition != getTouchUpPos(lastReceptor.AssociatedSource) ||
                    mouseUp.ScreenSpaceMouseDownPosition != getTouchDownPos(firstReceptor.AssociatedSource))
                    return false;

                return firstReceptor.MouseEvents.Count == 0;
            });

            AddAssert("all events dequeued", () => receptors.All(r => r.MouseEvents.Count == 0));

            bool assertMouseOnTouchChange(Touch touch, Vector2? lastPosition, bool expectsMouseMove)
            {
                var receptor = receptors[(int)touch.Source];

                if (expectsMouseMove)
                {
                    if (!(receptor.MouseEvents.TryDequeue(out MouseEvent me1) && me1 is MouseMoveEvent mouseMove))
                        return false;

                    if (mouseMove.ScreenSpaceMousePosition != touch.Position ||
                        (lastPosition != null && mouseMove.ScreenSpaceLastMousePosition != lastPosition.Value))
                        return false;
                }

                // Dequeue the "false drag" from first receptor to ensure there isn't any unexpected hidden event in this receptor.
                if (!(firstReceptor.MouseEvents.TryDequeue(out MouseEvent me2) && me2 is DragEvent mouseDrag))
                    return false;

                if (mouseDrag.Button != MouseButton.Left ||
                    mouseDrag.ScreenSpaceMousePosition != touch.Position ||
                    (lastPosition != null && mouseDrag.ScreenSpaceLastMousePosition != lastPosition.Value) ||
                    mouseDrag.ScreenSpaceMouseDownPosition != getTouchDownPos(firstReceptor.AssociatedSource))
                    return false;

                return receptor.MouseEvents.Count == 0;
            }
        }

        private class InputReceptor : CompositeDrawable
        {
            public readonly TouchSource AssociatedSource;

            public readonly Queue<TouchEvent> TouchEvents = new Queue<TouchEvent>();
            public readonly Queue<MouseEvent> MouseEvents = new Queue<MouseEvent>();

            public InputReceptor(TouchSource source)
            {
                AssociatedSource = source;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new SpriteText
                    {
                        X = 15f,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = source.ToString(),
                        Colour = Color4.Black,
                    },
                };
            }

            protected override bool Handle(UIEvent e)
            {
                switch (e)
                {
                    case TouchEvent te:
                        TouchEvents.Enqueue(te);
                        return !(e is TouchUpEvent);

                    case MouseDownEvent _:
                    case MouseMoveEvent _:
                    case DragEvent _:
                    case MouseUpEvent _:
                        MouseEvents.Enqueue((MouseEvent)e);
                        return !(e is MouseUpEvent);

                    // not worth enqueuing, just handle for receiving drag.
                    case DragStartEvent _:
                        return true;
                }

                return false;
            }
        }
    }
}
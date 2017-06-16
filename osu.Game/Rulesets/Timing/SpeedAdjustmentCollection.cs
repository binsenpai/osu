// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Caching;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Timing
{
    /// <summary>
    /// A collection of <see cref="SpeedAdjustmentContainer"/>s.
    ///
    /// <para>
    /// This container redirects any <see cref="DrawableHitObject"/>'s added to it to the <see cref="SpeedAdjustmentContainer"/>
    /// which provides the speed adjustment active at the start time of the hit object. Furthermore, this container provides the
    /// necessary <see cref="VisibleTimeRange"/> for the contained <see cref="SpeedAdjustmentContainer"/>s.
    /// </para>
    /// </summary>
    public class SpeedAdjustmentCollection : Container<SpeedAdjustmentContainer>
    {
        private readonly BindableDouble visibleTimeRange = new BindableDouble();
        /// <summary>
        /// Gets or sets the range of time that is visible by the length of this container.
        /// For example, only hit objects with start time less than or equal to 1000 will be visible with <see cref="VisibleTimeRange"/> = 1000.
        /// </summary>
        public Bindable<double> VisibleTimeRange
        {
            get { return visibleTimeRange; }
            set { visibleTimeRange.BindTo(value); }
        }

        protected override IComparer<Drawable> DepthComparer => new SpeedAdjustmentContainerReverseStartTimeComparer();

        private Cached layout = new Cached();

        /// <summary>
        /// Hit objects that are to be re-processed when <see cref="layout"/> is invalidated.
        /// </summary>
        private readonly Queue<DrawableHitObject> queuedHitObjects = new Queue<DrawableHitObject>();

        /// <summary>
        /// Adds a hit object to this <see cref="SpeedAdjustmentCollection"/>. The hit objects will be kept in a queue
        /// and will be processed when new <see cref="SpeedAdjustmentContainer"/>s are added to this <see cref="SpeedAdjustmentCollection"/>.
        /// </summary>
        /// <param name="hitObject">The hit object to add.</param>
        public void Add(DrawableHitObject hitObject)
        {
            queuedHitObjects.Enqueue(hitObject);
            layout.Invalidate();
        }

        public override void Add(SpeedAdjustmentContainer speedAdjustment)
        {
            speedAdjustment.VisibleTimeRange.BindTo(VisibleTimeRange);
            layout.Invalidate();
            base.Add(speedAdjustment);
        }

        protected override void Update()
        {
            base.Update();

            if (!layout.IsValid)
            {
                layout.Refresh(() =>
                {
                    // An external count is kept because hit objects that can't be added are re-queued
                    int count = queuedHitObjects.Count;
                    while (count-- > 0)
                    {
                        var hitObject = queuedHitObjects.Dequeue();

                        var target = adjustmentContainerFor(hitObject);
                        if (target == null)
                        {
                            // We can't add this hit object to a speed adjustment container yet, so re-queue it
                            // for re-processing when the layout next invalidated
                            queuedHitObjects.Enqueue(hitObject);
                            continue;
                        }

                        if (hitObject.RelativePositionAxes != target.ScrollingAxes)
                            throw new InvalidOperationException($"Make sure to set all {nameof(DrawableHitObject)}'s {nameof(RelativePositionAxes)} are equal to the correct axes of scrolling ({target.ScrollingAxes}).");

                        target.Add(hitObject);
                    }
                });
            }
        }

        /// <summary>
        /// Finds the <see cref="SpeedAdjustmentContainer"/> which provides the speed adjustment active at the start time
        /// of a hit object. If there is no <see cref="SpeedAdjustmentContainer"/> active at the start time of the hit object,
        /// then the first (time-wise) speed adjustment is returned.
        /// </summary>
        /// <param name="hitObject">The hit object to find the active <see cref="SpeedAdjustmentContainer"/> for.</param>
        /// <returns>The <see cref="SpeedAdjustmentContainer"/> active at <paramref name="hitObject"/>'s start time. Null if there are no speed adjustments.</returns>
        private SpeedAdjustmentContainer adjustmentContainerFor(DrawableHitObject hitObject) => Children.FirstOrDefault(c => c.CanContain(hitObject)) ?? Children.LastOrDefault();

        /// <summary>
        /// Finds the <see cref="SpeedAdjustmentContainer"/> which provides the speed adjustment active at a time.
        /// If there is no <see cref="SpeedAdjustmentContainer"/> active at the time, then the first (time-wise) speed adjustment is returned.
        /// </summary>
        /// <param name="time">The time to find the active <see cref="SpeedAdjustmentContainer"/> at.</param>
        /// <returns>The <see cref="SpeedAdjustmentContainer"/> active at <paramref name="time"/>. Null if there are no speed adjustments.</returns>
        private SpeedAdjustmentContainer adjustmentContainerAt(double time) => Children.FirstOrDefault(c => c.CanContain(time)) ?? Children.LastOrDefault();

        /// <summary>
        /// Compares two speed adjustment containers by their control point start time, falling back to creation order
        // if their control point start time is equal. This will compare the two speed adjustment containers in reverse order.
        /// </summary>
        private class SpeedAdjustmentContainerReverseStartTimeComparer : ReverseCreationOrderDepthComparer
        {
            public override int Compare(Drawable x, Drawable y)
            {
                var speedAdjustmentX = x as SpeedAdjustmentContainer;
                var speedAdjustmentY = y as SpeedAdjustmentContainer;

                // If either of the two drawables are not hit objects, fall back to the base comparer
                if (speedAdjustmentX?.ControlPoint == null || speedAdjustmentY?.ControlPoint == null)
                    return base.Compare(x, y);

                // Compare by start time
                int i = speedAdjustmentY.ControlPoint.StartTime.CompareTo(speedAdjustmentX.ControlPoint.StartTime);

                return i != 0 ? i : base.Compare(x, y);
            }
        }
    }
}
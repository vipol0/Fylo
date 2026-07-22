using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Fylo.Helpers
{
    public sealed class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);

        public double From
        {
            get => (double)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(nameof(From), typeof(double), typeof(GridLengthAnimation));

        public double To
        {
            get => (double)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(nameof(To), typeof(double), typeof(GridLengthAnimation));

        public IEasingFunction EasingFunction
        {
            get => (IEasingFunction)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        public static readonly DependencyProperty EasingFunctionProperty =
            DependencyProperty.Register(nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            double fromVal = From;
            double toVal = To;

            if (animationClock.CurrentProgress is double progress)
            {
                double eased = EasingFunction?.Ease(progress) ?? progress;
                double value = fromVal + (toVal - fromVal) * eased;
                return new GridLength(value);
            }

            return new GridLength(fromVal);
        }

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();
    }
}
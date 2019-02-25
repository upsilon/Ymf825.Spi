using System;

namespace Ymf825.IO
{
    public readonly struct GpioPin : IEquatable<GpioPin>
    {
        public static GpioPin None { get; } = default;

        public readonly int Number;

        public GpioPin(int pinNumber)
            => this.Number = pinNumber;

        public bool Equals(GpioPin other)
            => this.Number == other.Number;

        public override bool Equals(object obj)
            => obj is GpioPin other && this.Equals(other);

        public override int GetHashCode()
            => this.Number.GetHashCode();

        public static GpioPin FromNumber(int pinNumber)
            => new GpioPin(pinNumber);

        public static bool operator ==(GpioPin left, GpioPin right)
            => left.Equals(right);

        public static bool operator !=(GpioPin left, GpioPin right)
            => !left.Equals(right);
    }
}

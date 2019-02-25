using System;

namespace Ymf825.IO
{
    public readonly struct ChipPinConfig
    {
        public readonly GpioPin CsPin;
        public readonly GpioPin ResetPin;

        public ChipPinConfig(GpioPin csPin, GpioPin resetPin)
        {
            if (csPin == GpioPin.None)
                throw new ArgumentException("csPin must not be GpioPin.None.", nameof(csPin));

            this.CsPin = csPin;
            this.ResetPin = resetPin;
        }
    }
}

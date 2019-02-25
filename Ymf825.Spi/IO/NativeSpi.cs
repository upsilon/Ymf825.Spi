using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Device.Gpio;
using System.Device.Spi;
using System.Linq;
using System.Threading;

namespace Ymf825.IO
{
    public class NativeSpi : IYmf825
    {
        public bool SupportReadOperation => true;
        public bool SupportHardwareReset => true;

        public TargetChip AvailableChips { get; }
        public TargetChip CurrentTargetChips { get; private set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool AutoFlush
        {
            get => true;
            set { }
        }

        private readonly SpiDevice spiDevice;
        private readonly GpioController gpioController;
        private readonly IReadOnlyDictionary<TargetChip, ChipPinConfig> pinConfigMap;

        public NativeSpi(SpiDevice spiDevice, GpioController gpioController, IReadOnlyDictionary<TargetChip, ChipPinConfig> pinConfigMap)
        {
            if (pinConfigMap.Keys.Any(x => !x.IsSingleChip()))
                throw new ArgumentException($"{nameof(pinConfigMap)} key must be single chip", nameof(pinConfigMap));

            this.spiDevice = spiDevice;
            this.gpioController = gpioController;
            this.pinConfigMap = pinConfigMap;

            var availableChips = pinConfigMap.Keys.Aggregate(TargetChip.None, (sum, value) => sum | value);
            this.AvailableChips = availableChips;
            this.CurrentTargetChips = availableChips;

            this.OpenGpioPins();
            this.InvokeHardwareReset();
        }

        public void SetTarget(TargetChip chip)
        {
            if (!this.AvailableChips.HasFlag(chip))
                throw new ArgumentOutOfRangeException(nameof(chip));

            this.CurrentTargetChips = chip;
        }

        public void Write(byte command, byte data)
        {
            this.ThrowIfNoChipsSelected();

            byte[] buffer = null;
            try
            {
                buffer = ArrayPool<byte>.Shared.Rent(2);
                var bufferSpan = buffer.AsSpan().Slice(0, 2);

                bufferSpan[0] = command;
                bufferSpan[1] = data;

                this.EnableCs();
                this.spiDevice.Write(bufferSpan);
            }
            finally
            {
                this.DisableCs();
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void BurstWrite(byte command, byte[] data, int offset, int count)
            => this.BurstWrite(command, data.AsSpan().Slice(offset, count));

        public void BurstWrite(byte command, ReadOnlySpan<byte> data)
        {
            this.ThrowIfNoChipsSelected();

            byte[] buffer = null;
            try
            {
                var bufferLength = data.Length + 1;
                buffer = ArrayPool<byte>.Shared.Rent(bufferLength);
                var bufferSpan = buffer.AsSpan().Slice(0, bufferLength);

                bufferSpan[0] = command;
                data.CopyTo(bufferSpan.Slice(start: 1));

                this.EnableCs();
                this.spiDevice.Write(bufferSpan);
            }
            finally
            {
                this.DisableCs();
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public byte Read(byte command)
        {
            this.ThrowIfNoChipsSelected();

            if (!this.CurrentTargetChips.IsSingleChip())
                throw new InvalidOperationException("Cannot read from multiple chips at once.");

            try
            {
                this.EnableCs();
                this.spiDevice.WriteByte(command);
                return this.spiDevice.ReadByte();
            }
            finally
            {
                this.DisableCs();
            }
        }

        public void InvokeHardwareReset()
        {
            var allResetPins = this.pinConfigMap.Values.Select(x => x.ResetPin).Distinct();

            try
            {
                foreach (var pin in allResetPins)
                    this.gpioController.Write(pin, PinValue.High);

                Thread.Sleep(1);

                foreach (var pin in allResetPins)
                    this.gpioController.Write(pin, PinValue.Low);
            }
            finally
            {
                Thread.Sleep(1);

                foreach (var pin in allResetPins)
                    this.gpioController.Write(pin, PinValue.High);
            }
        }

        public void Flush()
        {
        }

        public void Dispose()
            => this.CloseGpioPins();

        private void OpenGpioPins()
        {
            foreach (var pinConfig in this.pinConfigMap.Values)
            {
                var csPin = pinConfig.CsPin;
                this.gpioController.OpenPin(csPin, PinMode.Output);
                this.gpioController.Write(csPin, PinValue.High);

                var resetPin = pinConfig.ResetPin;
                if (resetPin != GpioPin.None)
                {
                    this.gpioController.OpenPin(resetPin, PinMode.Output);
                    this.gpioController.Write(resetPin, PinValue.High);
                }
            }
        }

        private void CloseGpioPins()
        {
            foreach (var pinConfig in this.pinConfigMap.Values)
            {
                this.gpioController.ClosePin(pinConfig.CsPin);

                if (pinConfig.ResetPin != GpioPin.None)
                    this.gpioController.ClosePin(pinConfig.ResetPin);
            }
        }

        private void EnableCs()
            => this.WriteToCsPins(PinValue.Low);

        private void DisableCs()
            => this.WriteToCsPins(PinValue.High);

        private void WriteToCsPins(PinValue value)
        {
            foreach (var csPin in this.GetTargetCsPins())
                this.gpioController.Write(csPin, value);
        }

        private IEnumerable<GpioPin> GetTargetCsPins()
            => this.pinConfigMap
                .Where(x => this.CurrentTargetChips.HasFlag(x.Key))
                .Select(x => x.Value.CsPin);

        private void ThrowIfNoChipsSelected()
        {
            if (this.CurrentTargetChips == TargetChip.None)
                throw new InvalidOperationException("You must first select chips.");
        }
    }
}

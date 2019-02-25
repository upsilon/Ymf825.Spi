using System.Device.Gpio;

namespace Ymf825.IO
{
    internal static class GpioPinExtensions
    {
        public static void OpenPin(this GpioController gpioController, GpioPin pin, PinMode mode)
            => gpioController.OpenPin(pin.Number, mode);

        public static void ClosePin(this GpioController gpioController, GpioPin pin)
            => gpioController.ClosePin(pin.Number);

        public static void Write(this GpioController gpioController, GpioPin pin, PinValue value)
            => gpioController.Write(pin.Number, value);
    }
}

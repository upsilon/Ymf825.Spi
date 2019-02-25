// This is a derivative of nanase/ymf825 sample code used under MIT License.
//
// The original version can be found here:
//   https://github.com/nanase/ymf825/blob/1.1/ConsoleTestApp/Program.cs

// Copyright(c) 2017-2018 Tomona Nanase
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Device.Spi;
using System.Device.Spi.Drivers;
using System.Threading;
using Ymf825;
using Ymf825.Driver;
using Ymf825.IO;

namespace Examples
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var pinCS0 = GpioPin.FromNumber(25); // Pin 22 (BCM 25)
            var pinCS1 = GpioPin.FromNumber(26); // Pin 37 (BCM 26)
            var pinRST = GpioPin.FromNumber(16); // Pin 36 (BCM 16)

            var spiSettings = new SpiConnectionSettings(busId: 0, chipSelectLine: 0);
            var gpioPinMap = new Dictionary<TargetChip, ChipPinConfig>
            {
                [TargetChip.Board0] = new ChipPinConfig(csPin: pinCS0, resetPin: pinRST),
                [TargetChip.Board1] = new ChipPinConfig(csPin: pinCS1, resetPin: pinRST),
            };

            using (var spiDevice = new UnixSpiDevice(spiSettings))
            using (var gpioController = new GpioController(PinNumberingScheme.Logical))
            using (var ymf825Device = new NativeSpi(spiDevice, gpioController, gpioPinMap))
            {
                var driver = new Ymf825Driver(ymf825Device);
                driver.EnableSectionMode();

                Console.WriteLine("Software Reset");
                driver.ResetSoftware();

                SetupTones(driver);

                var index = 0;
                var score = new[]
                {
                    60, 62, 64, 65, 67, 69, 71, 72,
                    72, 74, 76, 77, 79, 81, 83, 84,
                    84, 83, 81, 79, 77, 76, 74, 72,
                    72, 71, 69, 67, 65, 64, 62, 60
                };

                while (true)
                {
                    const int noteOnTime = 250;
                    const int sleepTime = 0;

                    NoteOn(driver, score[index]);
                    Thread.Sleep(noteOnTime);
                    NoteOff(driver);

                    Thread.Sleep(sleepTime);

                    if (Console.KeyAvailable)
                        break;

                    index++;
                    if (index >= score.Length)
                        index = 0;
                }

                driver.ResetHardware();
            }
        }

        private static void SetupTones(Ymf825Driver driver)
        {
            Console.WriteLine("Tone Init");
            var tones = new ToneParameterCollection { [0] = ToneParameter.GetSine() };

            driver.Section(() =>
            {
                driver.WriteContentsData(tones, 0);
                driver.SetSequencerSetting(SequencerSetting.AllKeyOff | SequencerSetting.AllMute | SequencerSetting.AllEgReset |
                                           SequencerSetting.R_FIFOR | SequencerSetting.R_SEQ | SequencerSetting.R_FIFO);
            }, 1);

            driver.Section(() =>
            {
                driver.SetSequencerSetting(SequencerSetting.Reset);

                driver.SetToneFlag(0, false, true, true);
                driver.SetChannelVolume(31, true);
                driver.SetVibratoModuration(0);
                driver.SetFrequencyMultiplier(1, 0);
            });
        }

        private static void NoteOn(Ymf825Driver driver, int key)
        {
            Ymf825Driver.GetFnumAndBlock(key, out var fnum, out var block, out var correction);
            Ymf825Driver.ConvertForFrequencyMultiplier(correction, out var integer, out var fraction);
            var freq = Ymf825Driver.CalcFrequency(fnum, block);
            Console.WriteLine($"key: {key}, freq: {freq:f1} Hz, fnum: {fnum:f0}, block: {block}, correction: {correction:f3}, integer: {integer}, fraction: {fraction}");

            driver.Section(() =>
            {
                driver.SetVoiceNumber(0);
                driver.SetVoiceVolume(15);
                driver.SetFrequencyMultiplier(integer, fraction);
                driver.SetFnumAndBlock((int)Math.Round(fnum), block);
                driver.SetToneFlag(0, true, false, false);
            });
        }

        private static void NoteOff(Ymf825Driver driver)
            => driver.Section(() => driver.SetToneFlag(0, false, false, false));
    }
}

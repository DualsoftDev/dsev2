using System;
using System.Text.RegularExpressions;
using static PLC.Convert.LSCore.XGTTag.XGTDevice;
namespace PLC.Convert.LSCore.XGTTag
{
	/// <summary>
	/// The XGTParserXGI class provides utility functions for parsing XGI tags and extracting information about XGT devices.
	/// </summary>
	public static class XGTParserXGI
    {
        private static string regexXGI1 = @"^%([IQUMLKFNRAW])X([\d]+)$";
        private static string regexXGI2 = @"^%([IQU])([BWDLX])(\d+)\.(\d+)\.(\d+)$";
        private static string regexXGI3 = @"^%([IQMLKFNRAW])([BWDL])(\d+)$";
        private static string regexXGI4 = @"^%([IQMLKFNRAW])([BWDL])(\d+)\.(\d+)$";
        private static string regexXGI5 = @"^%([IQT])S([BWDLX])(\d+)\.(\d+)\.(\d+)$";//세이프티

		/// <summary>
		/// Parses the given tag string and returns a tuple containing device, size, and bit offset information.
		/// </summary>
		/// <param name="name">The tag name to parse.</param>
		/// <returns>A tuple with the device identifier, the data type size, and the bit offset.</returns>
		public static Tuple<string, XGTDeviceSize, int> LsTagXGIPattern(string name)
        {
            string tag = name.ToUpper();

            // Match regex patterns here and perform corresponding actions
            if (Regex.Match(tag, regexXGI1).Success)
            {
                Match match = Regex.Match(tag, regexXGI1);
                string device = match.Groups[1].Value;
                int bit = int.Parse(match.Groups[2].Value);
                return XGTParserUtil.CreateTagInfo(tag, device, XGTDeviceSize.Bit, bit);
            }
            else if (Regex.Match(tag, regexXGI2).Success)
            {
                Match match = Regex.Match(tag, regexXGI2);
                string device = match.Groups[1].Value;
                string dataTypeStr = match.Groups[2].Value;
                int file = int.Parse(match.Groups[3].Value);
                int element = int.Parse(match.Groups[4].Value);
                int bit = int.Parse(match.Groups[5].Value);
                XGTDeviceSize dataType = getType(tag, dataTypeStr);

                int baseStep = device == "U" ? 512 * 16 : 64 * 16;
                int slotStep = device == "U" ? 512      : 64;


                int totalBitOffset = file * baseStep + element * slotStep + bit;
                return XGTParserUtil.CreateTagInfo(tag, device, dataType, totalBitOffset);
            }
            else if (Regex.Match(tag, regexXGI3).Success)
            {
                Match match = Regex.Match(tag, regexXGI3);
                string device = match.Groups[1].Value;
                string dataTypeStr = match.Groups[2].Value;
                int offset = int.Parse(match.Groups[3].Value);

                XGTDeviceSize dataType = getType(tag, dataTypeStr);
                var byteOffset = offset * XGTParserUtil.GetByteLength(dataType);
                int totalBitOffset = byteOffset * 8;
                return XGTParserUtil.CreateTagInfo(tag, device, dataType, totalBitOffset);
            }
            else if (Regex.Match(tag, regexXGI4).Success)
            {
                Match match = Regex.Match(tag, regexXGI4);
                string device = match.Groups[1].Value;
                string dataTypeStr = match.Groups[2].Value;
                int offset = int.Parse(match.Groups[3].Value);
                int bit = int.Parse(match.Groups[4].Value);
                XGTDeviceSize dataType = getType(tag, dataTypeStr);

                var bitOffset = offset * XGTParserUtil.GetByteLength(dataType) * 8;
                var totalBitOffset = bitOffset + bit;

                return XGTParserUtil.CreateTagInfo(tag, device, XGTDeviceSize.Bit, totalBitOffset);
            }
            else if (Regex.Match(tag, regexXGI5).Success)
            {
                Match match = Regex.Match(tag, regexXGI2);
                string device = match.Groups[1].Value;
                string dataTypeStr = match.Groups[2].Value;
                int file = int.Parse(match.Groups[3].Value);
                int element = int.Parse(match.Groups[4].Value);
                int bit = int.Parse(match.Groups[5].Value);
                XGTDeviceSize dataType = getType(tag, dataTypeStr);
                var byteOffset = element * XGTParserUtil.GetByteLength(dataType);

                int baseStep =  64 * 16;
                int slotStep =  64;

                int totalBitOffset = file * baseStep + byteOffset * slotStep + bit;
                return XGTParserUtil.CreateTagInfo(tag, device, dataType, totalBitOffset);
            }
            Console.WriteLine($"Failed to XGI parse tag : {tag}");
            return null;
        }
		/// <summary>
		/// Determines the XGT device size type based on the string representation.
		/// </summary>
		/// <param name="tag">The tag string.</param>
		/// <param name="dataTypeStr">The string representation of the data type.</param>
		/// <returns>The corresponding XGTDeviceSize type.</returns>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if an invalid device type is encountered.</exception>
		private static XGTDeviceSize getType(string tag, string dataTypeStr)
        {
            XGTDeviceSize dataType;
            switch (dataTypeStr)
            {
                case "X":
                    dataType = XGTDeviceSize.Bit;
                    break;
                case "B":
                    dataType = XGTDeviceSize.Byte;
                    break;
                case "W":
                    dataType = XGTDeviceSize.Word;
                    break;
                case "D":
                    dataType = XGTDeviceSize.DWord;
                    break;
                case "L":
                    dataType = XGTDeviceSize.LWord;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Invalid device type in tag: {tag}");
            }

            return dataType;
        }
    }
}
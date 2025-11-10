using System;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text.RegularExpressions;
using static PLC.Convert.LSCore.XGTTag.XGTDevice;

namespace PLC.Convert.LSCore.XGTTag
{
	/// <summary>
	/// The XGTParserXGK class provides utility functions for parsing XGK tags and extracting information about XGT devices specific to the XGK series.
	/// </summary>
	public static class XGTParserXGK
    {
        static string regexXGK1 = @"^([PMKFTCLNDRZR])(\d+)([\da-fA-F])$";
        static string regexXGK2 = @"^([PMKFTCLNDRZR])(\d+)\.([\da-fA-F])$";
        static string regexXGK3 = @"^([S])(\d+)\.(\d+)$";
        static string regexXGK4 = @"^([U])(\d+)\.(\d+)$";
        static string regexXGK5 = @"^([U])(\d+)\.(\d+)\\.([\da-fA-F])$";
        static string regexXGK7 = @"^(ZR)(\d+)$";

		/// <summary>
		/// Parses the given XGK tag string and returns a tuple containing device, size, and bit offset information.
		/// The method matches the tag against various regex patterns to extract the relevant information.
		/// </summary>
		/// <param name="tag">The tag name to parse.</param>
		/// <param name="isBit">A boolean indicating if the tag refers to a bit or not.</param>
		/// <returns>A tuple with the device identifier, the data type size, and the bit offset. Returns null if parsing fails.</returns>
		public static Tuple<string, XGTDeviceSize, int> LsTagXGKPattern(string tag, bool isBit)
        {
            tag = tag.ToUpper().Split('[').First(); //ZR123321[Z01]

            if (Regex.Match(tag, regexXGK1).Success)
            {
                Match m = Regex.Match(tag, regexXGK1);
                string device = m.Groups[1].Value;
                int word = int.Parse(m.Groups[2].Value);
                int hexaBit = int.Parse(m.Groups[3].Value, System.Globalization.NumberStyles.HexNumber);
                int totalBitOffset = isBit ? word * 16 + hexaBit : (word * 10 + hexaBit) * 16;    
                return XGTParserUtil.CreateTagInfo(tag, device, isBit ? XGTDeviceSize.Bit : XGTDeviceSize.Word, totalBitOffset);
            }
            else if (Regex.Match(tag, regexXGK2).Success)
            {
                Match m = Regex.Match(tag, regexXGK2);
                string device = m.Groups[1].Value;
                int word = int.Parse(m.Groups[2].Value);
                int hexaBit = int.Parse(m.Groups[3].Value, System.Globalization.NumberStyles.HexNumber);
                int totalBitOffset = isBit ? word * 16 + hexaBit : (word * 10 + hexaBit) * 16;    
                return XGTParserUtil.CreateTagInfo(tag, device, XGTDeviceSize.Bit, totalBitOffset);
            }
            else if (Regex.Match(tag, regexXGK4).Success)
            {
                Match m = Regex.Match(tag, regexXGK4);
                string device = m.Groups[1].Value;
                int file = int.Parse(m.Groups[2].Value);
                int sub = int.Parse(m.Groups[3].Value);
                int totalBitOffset = file * 32 * 16 + sub * 16;
                return XGTParserUtil.CreateTagInfo(tag, device, XGTDeviceSize.Word, totalBitOffset);
            }
            else if (Regex.Match(tag, regexXGK5).Success)
            {
                Match m = Regex.Match(tag, regexXGK5);
                string device = m.Groups[1].Value;
                int file = int.Parse(m.Groups[2].Value);
                int sub = int.Parse(m.Groups[3].Value);
                int hexaBit = int.Parse(m.Groups[4].Value, System.Globalization.NumberStyles.HexNumber);
                int totalBitOffset = file * 32 * 16 + sub * 16 + hexaBit;
                return XGTParserUtil.CreateTagInfo(tag, device, XGTDeviceSize.Bit, totalBitOffset);
            }
            else if (Regex.Match(tag, regexXGK3).Success)
            {
                Match m = Regex.Match(tag, regexXGK3);
                string device = m.Groups[1].Value;
                int word = int.Parse(m.Groups[2].Value);
                int bit = int.Parse(m.Groups[3].Value);
                int totalBitOffset = word * 16 + bit;
                return XGTParserUtil.CreateTagInfo(tag, device, XGTDeviceSize.Bit, totalBitOffset);
            }
            else if (Regex.Match(tag, regexXGK7).Success)
            {
                Match m = Regex.Match(tag, regexXGK7);
                string device = m.Groups[1].Value;
                int word = int.Parse(m.Groups[2].Value);
                int totalBitOffset = word * 16;
                return XGTParserUtil.CreateTagInfo(tag, device, XGTDeviceSize.Word, totalBitOffset);
            }

            Console.WriteLine($"Failed to XGK parse tag : {tag}");
            return null;
        }

     
    }
}

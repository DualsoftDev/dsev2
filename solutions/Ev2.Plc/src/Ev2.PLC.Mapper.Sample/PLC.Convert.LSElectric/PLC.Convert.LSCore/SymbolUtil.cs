using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using PLC.Convert.LSCore.Expression;
using System.Linq;
using PLC.Convert.LSCore.XGTTag;
using static PLC.Convert.LSCore.XGTTag.XGTDevice;

namespace PLC.Convert.LSCore
{
	/// <summary>
	/// The SymbolUtil class provides utility functions for handling symbols, including conversion between data types, keys, and device creation.
	/// </summary>
	public static class SymbolUtil
    {
		/// <summary>
		/// Converts a string representation of a data type to its corresponding SymbolDataType enum.
		/// </summary>
		/// <param name="typeName">The name of the type.</param>
		/// <returns>The corresponding SymbolDataType.</returns>
		public static SymbolDataType GetDataTypeFromString(string typeName)
        {
            var name = CallParaUtil.GetXGIXGKType(typeName.ToUpper());
            // Try to parse the input string as an enum of type SymbolDataType
            bool success = Enum.TryParse(name, true, out SymbolDataType result);

            if (!success && typeName.Contains(" OF ")) //ARRAY[0..1] OF BOOL   
                return SymbolDataType.XGI_ARRAY;
            else 
                return success ? result : SymbolDataType.NONE;
        }
		/// <summary>
		/// Converts an integer index to its corresponding SymbolDataType enum.
		/// </summary>
		/// <param name="index">The index representing the data type.</param>
		/// <returns>The corresponding SymbolDataType.</returns>
		public static SymbolDataType GetDataTypeFromIndex(int index)
        {
            return (SymbolDataType)index;
        }
		/// <summary>
		/// Gets the size of the specified symbol data type.
		/// </summary>
		/// <param name="symbolDataType">The symbol data type.</param>
		/// <returns>The size of the symbol data type.</returns>
		public static int GetSizeType(SymbolDataType symbolDataType)
        {
            switch (symbolDataType)
            {
                case SymbolDataType.NONE: return 0;
                case SymbolDataType.NIBBLE: return 4;
                case SymbolDataType.STRING: return 64;
                case SymbolDataType.DT: return 64;
                case SymbolDataType.TIME: return 64;
                case SymbolDataType.BITWORD: return 16;
                case SymbolDataType.DATE: return 64;
                case SymbolDataType.TOD: return 64;
                case SymbolDataType.TIMERO_1: return 64;
                case SymbolDataType.TIMER1: return 64;
                case SymbolDataType.TIMER10: return 64;
                case SymbolDataType.TIMER100: return 64;
                case SymbolDataType.COUNTER: return 64;
                case SymbolDataType.ARRAY: return 64;
                case SymbolDataType.FB_INST: return 64;
                case SymbolDataType.FUN_INST: return 64;
                case SymbolDataType.BIT: return 1;
                case SymbolDataType.BYTE: return 8;
                case SymbolDataType.INT:
                case SymbolDataType.USINT:
                case SymbolDataType.SINT:
                case SymbolDataType.UINT:
                case SymbolDataType.WORD: return 16;
                case SymbolDataType.DWORD:
                case SymbolDataType.UDINT:
                case SymbolDataType.DINT: return 32;
                case SymbolDataType.REAL:
                case SymbolDataType.LREAL:
                case SymbolDataType.LINT:
                case SymbolDataType.ULINT:
                case SymbolDataType.LWORD: return 64;


                case SymbolDataType.XGI_NONE: return 0;
                case SymbolDataType.XGI_BOOL: return 1;
                case SymbolDataType.XGI_BYTE: return 8;
                case SymbolDataType.XGI_WORD: return 16;
                case SymbolDataType.XGI_DWORD: return 32;
                case SymbolDataType.XGI_LWORD: return 64;


                case SymbolDataType.XGI_SINT: return 8;
                case SymbolDataType.XGI_INT: return 16;
                case SymbolDataType.XGI_DINT: return 32;
                case SymbolDataType.XGI_LINT: return 64;
                case SymbolDataType.XGI_USINT: return 8;
                case SymbolDataType.XGI_UINT: return 16;
                case SymbolDataType.XGI_UDINT: return 32;
                case SymbolDataType.XGI_ULINT: return 64;


                case SymbolDataType.XGI_REAL: 
                case SymbolDataType.XGI_LREAL:
                case SymbolDataType.XGI_TIME: 
                case SymbolDataType.XGI_DATE:
                case SymbolDataType.XGI_TIME_OF_DAY:
                case SymbolDataType.XGI_DATE_AND_TIME:
                case SymbolDataType.XGI_STRING:
                case SymbolDataType.XGI_WSTRING:
                case SymbolDataType.XGI_ARRAY:
                case SymbolDataType.XGI_STRUCT:
                case SymbolDataType.XGI_FB_INST: return 64;
                case SymbolDataType.XGI_SAFEBOOL: return 1;
                case SymbolDataType.XGI_USERDATA: return 64;


                default:
                    throw new Exception($"Error : {symbolDataType}");
            }
        }
		/// <summary>
		/// Converts a SymbolDataType to its corresponding XGTDeviceSize.
		/// </summary>
		/// <param name="symbolDataType">The symbol data type.</param>
		/// <returns>The corresponding XGTDeviceSize.</returns>
		public static XGTDeviceSize GetXGTSizeType(SymbolDataType symbolDataType)
        {
            switch (GetSizeType(symbolDataType))
            {
                case 0: throw new Exception($"Error : {symbolDataType}");
                case 1: return XGTDeviceSize.Bit;
                case 8: return XGTDeviceSize.Byte;
                case 16: return XGTDeviceSize.Word;
                case 32: return XGTDeviceSize.DWord;
                case 64: return XGTDeviceSize.LWord;
                default:
                    throw new Exception($"Error : {symbolDataType}");
            }
        }
		/// <summary>
		/// Converts an XGTDeviceSize to its corresponding SymbolDataType.
		/// </summary>
		/// <param name="sizeType">The XGT device size type.</param>
		/// <returns>The corresponding SymbolDataType.</returns>
		public static SymbolDataType GetDataType(XGTDeviceSize sizeType)
        {
            switch (sizeType)
            {
                case XGTDeviceSize.Bit: return   CallParaUtil.IsXGIRuntime ?  SymbolDataType.XGI_BOOL    :SymbolDataType.BIT  ;
                case XGTDeviceSize.Byte: return CallParaUtil.IsXGIRuntime ?  SymbolDataType.XGI_BYTE     :SymbolDataType.BYTE  ;
                case XGTDeviceSize.Word: return CallParaUtil.IsXGIRuntime ?   SymbolDataType.XGI_WORD    :SymbolDataType.WORD  ;
                case XGTDeviceSize.DWord: return CallParaUtil.IsXGIRuntime ?   SymbolDataType.XGI_DWORD  :SymbolDataType.DWORD ;
                case XGTDeviceSize.LWord: return CallParaUtil.IsXGIRuntime ? SymbolDataType.XGI_LWORD : SymbolDataType.LWORD;
                default:
                    throw new Exception($"Error : {sizeType}");
            }
        }

		/// <summary>
		/// Generates a key string from various parameters.
		/// </summary>
		/// <param name="task">The task identifier.</param>
		/// <param name="name">The name identifier.</param>
		/// <param name="sdt">The symbol data type.</param>
		/// <param name="isGlobal">Flag indicating if the symbol is global.</param>
		/// <returns>A string key composed of the provided parameters.</returns>
		public static string GetKey(string task, string name, SymbolDataType sdt, bool isGlobal)
        {
            return String.Join("|", new string[] { task, name, sdt.ToString(), isGlobal ? "G" : "L" });
        }

		/// <summary>
		/// Generates a key for user or function block data.
		/// </summary>
		/// <param name="task">The task identifier.</param>
		/// <param name="name">The name identifier.</param>
		/// <returns>A string key composed of the task and name.</returns>
		public static string GetUserOrFBDataKey(string task, string name)
        {
            return String.Join("|", new string[] { task, name });
        }

		/// <summary>
		/// Generates an auto device key from the task and name, handling specific cases like '.Q' and '.RM'.
		/// </summary>
		/// <param name="task">The task identifier.</param>
		/// <param name="name">The name identifier.</param>
		/// <returns>A string representing the auto device key.</returns>
		public static string GetAutoDeviceKey(string task, string name)
        {
            return String.Join("|", new string[] { task, name.Split('[').First().TrimEnd('Q').TrimEnd('.') });
        }
		/// <summary>
		/// Generates a key for a terminal based on its task, x, and y coordinates.
		/// </summary>
		/// <param name="task">The task identifier.</param>
		/// <param name="x">The x-coordinate.</param>
		/// <param name="y">The y-coordinate.</param>
		/// <returns>A string key for the terminal.</returns>
		public static string GetTerminalKey(string task, int x, int y)
        {
            return String.Join(";", new string[] { task, x.ToString(), y.ToString() });
        }
		/// <summary>
		/// Creates an XGTDevice object based on the provided symbol.
		/// </summary>
		/// <param name="s">The symbol from which to create the device.</param>
		/// <returns>The created XGTDevice, or null if not applicable.</returns>
		public static void CreateXGTDevice(Symbol s)
        {
            if (s.XGTDevice != null || s.SymbolDataType == SymbolDataType.NONE) return ;

            var offset = s.BitStartOffset;
            var deviceChar = s.Device[0];
            if (s.Device == "ZR") deviceChar = 'W';

            switch (GetXGTSizeType(s.SymbolDataType))
            {
                case XGTDeviceSize.Bit:
                    s.XGTDevice =  new XGTDeviceBit(deviceChar, offset); return;
                case XGTDeviceSize.Byte:
                    s.XGTDevice =  new XGTDeviceByte(deviceChar, offset); return;
                case XGTDeviceSize.Word:     
                    s.XGTDevice =  new XGTDeviceWord(deviceChar, offset); return;
                case XGTDeviceSize.DWord:
                    s.XGTDevice =  new XGTDeviceDWord(deviceChar, offset); return;
                case XGTDeviceSize.LWord:
                    s.XGTDevice =  new XGTDeviceLWord(deviceChar, offset); return;
                default:
                    throw new Exception($"Size value error : current {s.SymbolDataType}");
            }
        }
		/// <summary>
		/// Finds and returns terminals referencing a specific symbol.
		/// </summary>
		/// <param name="terminals">The collection of terminals to search through.</param>
		/// <param name="find">The symbol to find references to.</param>
		/// <returns>An enumerable of terminals referencing the specified symbol.</returns>
		public static IEnumerable<Terminal> FindRefTerminals(IEnumerable<Terminal> terminals, Symbol find)
        {
            var end = find.BitEndOffset;
            var st = find.BitStartOffset;
            var ts = terminals
                              .Where(s => s.Symbol.Device == find.Device)
                              .Where(s => s.Symbol.BitStartOffset <= end && s.Symbol.BitEndOffset >= st)
                              .Where(s => s.Symbol != find);
            return ts;
        }
    }
}
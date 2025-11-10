using System;
using System.Configuration;
using System.Drawing;
using System.Linq;

namespace PLC.Convert.LSCore
{
    public interface ISymbolArray { }
    public class BIT : ISymbolArray { }
    public class BYTE : ISymbolArray { }
    public class WORD : ISymbolArray { }
    public class DWORD : ISymbolArray { }
    public class LWORD : ISymbolArray { }
    public class SINT : ISymbolArray { }
    public class INT : ISymbolArray { }
    public class LINT : ISymbolArray { }
    public class USINT : ISymbolArray { }
    public class UINT : ISymbolArray { }
    public class UDINT : ISymbolArray { }
    public class ULINT : ISymbolArray { }
    public class REAL : ISymbolArray { }
    public class LREAL : ISymbolArray { }
    public class TIME : ISymbolArray { }
    public class DATE : ISymbolArray { }
    public class TOD : ISymbolArray { }
    public class DT : ISymbolArray { }
    public class STRING : ISymbolArray { }
    public class USERDATA : ISymbolArray { }

    public class SymbolArray<T> : ISymbolArray
    {
        private T[] dataArray1D;
        private T[,] dataArray2D;
        private T[,,] dataArray3D;

        public SymbolArray(int size1)
        {
            dataArray1D = new T[size1];
        }

        public SymbolArray(int size1, int size2)
        {
            dataArray2D = new T[size1, size2];
        }

        public SymbolArray(int size1, int size2, int size3)
        {
            dataArray3D = new T[size1, size2, size3];
        }

        // Methods for setting and getting values in different dimensions
        public void SetValue(int index, T value)
        {
            // Implement SetValue logic for 1D array
            if (IsIndexValid(index, dataArray1D))
            {
                dataArray1D[index] = value;
            }
            else
            {
                // Handle invalid index
                Console.WriteLine("Invalid index for 1D array!");
            }
        }

        public T GetValue(int index)
        {
            // Implement GetValue logic for 1D array
            if (IsIndexValid(index, dataArray1D))
            {
                return dataArray1D[index];
            }
            else
            {
                // Handle invalid index
                Console.WriteLine("Invalid index for 1D array!");
                return default(T);
            }
        }

        public void SetValue(int index1, int index2, T value)
        {
            // Implement SetValue logic for 2D array
            if (IsIndexValid(index1, index2, dataArray2D))
            {
                dataArray2D[index1, index2] = value;
            }
            else
            {
                // Handle invalid indices
                Console.WriteLine("Invalid indices for 2D array!");
            }
        }

        public T GetValue(int index1, int index2)
        {
            // Implement GetValue logic for 2D array
            if (IsIndexValid(index1, index2, dataArray2D))
            {
                return dataArray2D[index1, index2];
            }
            else
            {
                // Handle invalid indices
                Console.WriteLine("Invalid indices for 2D array!");
                return default(T);
            }
        }

        public void SetValue(int index1, int index2, int index3, T value)
        {
            // Implement SetValue logic for 3D array
            if (IsIndexValid(index1, index2, index3, dataArray3D))
            {
                dataArray3D[index1, index2, index3] = value;
            }
            else
            {
                // Handle invalid indices
                Console.WriteLine("Invalid indices for 3D array!");
            }
        }

        public T GetValue(int index1, int index2, int index3)
        {
            // Implement GetValue logic for 3D array
            if (IsIndexValid(index1, index2, index3, dataArray3D))
            {
                return dataArray3D[index1, index2, index3];
            }
            else
            {
                // Handle invalid indices
                Console.WriteLine("Invalid indices for 3D array!");
                return default(T);
            }
        }

        // Check if the provided indices are within bounds for different array dimensions
        private bool IsIndexValid(int index, T[] array)
        {
            return index >= 0 && index < array.Length;
        }

        private bool IsIndexValid(int index1, int index2, T[,] array)
        {
            return index1 >= 0 && index1 < array.GetLength(0) &&
                   index2 >= 0 && index2 < array.GetLength(1);
        }

        private bool IsIndexValid(int index1, int index2, int index3, T[,,] array)
        {
            return index1 >= 0 && index1 < array.GetLength(0) &&
                   index2 >= 0 && index2 < array.GetLength(1) &&
                   index3 >= 0 && index3 < array.GetLength(2);
        }

        // Other methods or logic for the SymbolArray class can be added here...
    }


    public static class SymbolArrayUtil
    {
        private static SymbolArray<T> Create<T>(int[] dimensions)
        {
            if (dimensions.Length == 1)
            {
                return new SymbolArray<T>(dimensions[0]);
            }
            else if (dimensions.Length == 2)
            {
                return new SymbolArray<T>(dimensions[0], dimensions[1]);
            }
            else if (dimensions.Length == 3)
            {
                return new SymbolArray<T>(dimensions[0], dimensions[1], dimensions[2]);
            }

            throw new ArgumentException("Invalid type or dimensions specified.");
        }
        public static ISymbolArray CreateSymbolArray(string name)
        {
            string[] parts = name.Split(' ');
            var type = SymbolUtil.GetDataTypeFromString(parts.Last());
            var arraysText = parts[0].Replace("ARRAY[", "").Replace("0..", "").TrimEnd(']');   
            // Extracting the dimensions
            int[] dimensions = Array.ConvertAll(arraysText.Split(','), int.Parse);
            switch (type)
            {
                case SymbolDataType.BIT: return Create<BIT>(dimensions);
                case SymbolDataType.BYTE: return Create<BYTE>(dimensions);
                case SymbolDataType.WORD: return Create<WORD>(dimensions);
                case SymbolDataType.DWORD: return Create<DWORD>(dimensions);
                case SymbolDataType.LWORD: return Create<LWORD>(dimensions);
                case SymbolDataType.SINT: return Create<SINT>(dimensions);
                case SymbolDataType.INT: return Create<INT>(dimensions);
                case SymbolDataType.LINT: return Create<LINT>(dimensions);
                case SymbolDataType.USINT: return Create<USINT>(dimensions);
                case SymbolDataType.UINT: return Create<UINT>(dimensions);
                case SymbolDataType.UDINT: return Create<UDINT>(dimensions);
                case SymbolDataType.ULINT: return Create<ULINT>(dimensions);
                case SymbolDataType.REAL: return Create<REAL>(dimensions);
                case SymbolDataType.LREAL: return Create<LREAL>(dimensions);
                case SymbolDataType.TIME: return Create<TIME>(dimensions);
                case SymbolDataType.DATE: return Create<DATE>(dimensions);
                case SymbolDataType.TOD: return Create<TOD>(dimensions);
                case SymbolDataType.DT: return Create<DT>(dimensions);
                case SymbolDataType.STRING: return Create<STRING>(dimensions);
                //SymbolDataType.USERDATA
                default:  return Create<USERDATA>(dimensions);
            }
        }
    }
}

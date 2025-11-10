using System;

namespace PLC.Convert.LSCore
{
    /// <summary>
    /// Represents a symbol in the program.
    /// </summary>
    public enum SymbolDataType
    {
        //xgk 0 ~
        NONE = 0,
        BIT = 1,
        NIBBLE = 2,
        BYTE = 3,
        WORD = 4,
        DWORD = 5,
        LWORD = 6,
        REAL = 7,
        LREAL = 8,
        INT = 9,
        DINT = 10,
        LINT = 11,
        STRING = 12,
        DT = 13,
        TIME = 14,
        BITWORD = 15,
        SINT = 16,
        USINT = 17,
        UINT = 18,
        UDINT = 19,
        ULINT = 20,
        DATE = 21,
        TOD = 22,
        TIMERO_1 = 23,
        TIMER1 = 24,
        TIMER10 = 25,
        TIMER100 = 26,
        COUNTER = 27,
        ARRAY = 28,
        FB_INST = 29,
        FUN_INST = 30,
        USERDATA = 31, //추가
        //xgi 100 ~
        XGI_NONE = 100,
        XGI_BOOL = 101,
        XGI_BYTE = 102,
        XGI_WORD = 103,
        XGI_DWORD = 104,
        XGI_LWORD = 105,
        XGI_SINT = 106,
        XGI_INT = 107,
        XGI_DINT = 108,
        XGI_LINT = 109,
        XGI_USINT = 110,
        XGI_UINT = 111,
        XGI_UDINT = 112,
        XGI_ULINT = 113,
        XGI_REAL = 114,
        XGI_LREAL = 115,
        XGI_TIME = 116,
        XGI_DATE = 117,
        XGI_TIME_OF_DAY = 118,
        XGI_DATE_AND_TIME = 119,
        XGI_STRING = 120,
        XGI_WSTRING = 121,
        XGI_ARRAY = 122,
        XGI_STRUCT = 123,
        XGI_FB_INST = 124,
        XGI_SAFEBOOL = 125,
        XGI_USERDATA = 126, //추가
    }

    public enum XGIDataType
    {
       
    }
}

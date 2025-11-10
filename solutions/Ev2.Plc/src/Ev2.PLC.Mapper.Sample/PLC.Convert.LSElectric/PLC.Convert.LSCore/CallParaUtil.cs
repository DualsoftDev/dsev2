using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PLC.Convert.LSCore
{

    public static class CallParaUtil
    {
        public static bool IsXGIRuntime = false;

        
        public static string GetSymbolDataTypeText(SymbolDataType t) => t.ToString().Replace("XGI_", "");    
        public static string GetXGIXGKType(string name) => CallParaUtil.IsXGIRuntime?  $"XGI_{name}" : name;   

        public static CallPara GetCallPara(List<CallPara> xs, string operand, Expression.OpType operation, bool stLine)
        {
            if (operand == "")
            {
                Debug.WriteLine($"operand is empty: OpType {operation}"); //load_on 비어있음 나머지도 확인필요
            }


            //동일 이름이 있을 경우 IL 위치를 알수 없기에 위치상 가장 오른쪽 기준
            var callPara =
                    xs.Where(w => !w.IsAssigned).OrderBy(f => f.XPoint)
                      .FirstOrDefault(f => operand == f.TagName || operand == f.Address);


            if (callPara == null)
            {
                if (!stLine)
                {
                    Debug.WriteLine($" error getCallPara {operand}");
                }

                callPara = xs.OrderBy(f => f.YPoint).Last();
            }else
                callPara.IsAssigned = true;


            return callPara;
        }
    }
}

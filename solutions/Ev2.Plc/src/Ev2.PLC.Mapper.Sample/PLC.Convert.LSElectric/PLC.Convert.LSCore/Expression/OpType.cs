namespace PLC.Convert.LSCore.Expression
{

    /// <summary>
    /// Enumeration representing different operation types.
    /// </summary>
    public enum OpType
    {
        None = 0
        , END
        , INIT_DONE //org_:_INIT_DONE
        , CALL
        , JMP
        , SBRT
        , RET
        , FOR
        , NEXT
        , BREAK
        , NOP
        , LABEL
        , NOT
        , MPUSH
        , MLOAD
        , MPOP
        , LOAD
        , LOAD_FB
        , LOAD_NOT
        , LOADN_NOT
        , LOADP_NOT
        , AND
        , AND_FB
        , AND_NOT
        , OR
        , OR_FB
        , OR_NOT
        , OUT_FB // FB VAR_OUT
        , OUT
        , OUT_NOT
        , SET
        , RST
        , LOADP
        , LOADN     //for문 load (일반 로드동일 취급)
        , ANDP
        , ANDN
        , ANDN_NOT
        , ANDP_NOT
        , ORN_NOT
        , ORP_NOT
        , ORP
        , ORN
        , OUTP
        , OUTN
        , FF        //출력비트 반전
        , ST_JMP    //org_:_ST_JMP
        , ST_LABEL  //org_:_ST_LABEL
        , AND_LOAD
        , OR_LOAD
        , LOAD_ON
        , LOAD_BR
        , LOAD_ON2
        , LOAD_S
        , LOAD_NOT_S
        , LOADP_S
        , LOADN_S
        , AND_S
        , AND_NOT_S
        , ANDP_S
        , ANDN_S
        , OR_S
        , OR_NOT_S
        , ORP_S
        , ORN_S
        , OUT_S
        , SET_S
        , CMT//설명문
        , OUTCMT//설명문
        , REM//주석코드 실행안함
        , UDF_RUNG_BEGIN // 실행안함
        , R_EDGE
        , F_EDGE

    }
}

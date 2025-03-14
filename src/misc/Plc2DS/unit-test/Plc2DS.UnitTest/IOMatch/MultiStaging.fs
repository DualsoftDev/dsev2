module MultiStaging


(*
    (FLOW)_(DEVICE)_(확실한 ACTION)$ 패턴을 먼저 찾아서
        - DEVICE 영역을 가변적으로 가져 갈 수 있도록 한다.
        - 확실한 ACTION 이름 : "_ADV$", "_RET$" 등
        - e.g "S303_SOL_SV_RH_B_PNL_CLP_ADV" 를 통해 확실한 ACTION 명인 "_ADV" 앞에서 FLOW 와 DEVICE 명을 구분
            "S303", "SOL_SV_RH_B_PNL_CLP"
            -> ErasePattern 적용: "SOL_SV_RH_B_PNL_CLP" -> "SV_RH_B_PNL_CLP"
            -> 다음과 같은 pattern 에 대해서


                "S303_M_SV_RH_B_PNL_CLP_ADV_END"       =>         "S303", "SV_RH_B_PNL_CLP", "ADV_END"
                "S303_M_SV_RH_B_PNL_CLP_ADV_ERR"       =>         "S303", "SV_RH_B_PNL_CLP", "ADV_ERR"
                "S303_M_SV_RH_B_PNL_CLP_ADV_HMI_LAMP"  =>         "S303", "SV_RH_B_PNL_CLP", "ADV_HMI_LAMP"
                "S303_M_SV_RH_B_PNL_CLP_ADV_LAMP"      =>         "S303", "SV_RH_B_PNL_CLP", "ADV_LAMP"
                "S303_M_SV_RH_B_PNL_CLP_A_ADV_AUX"     =>         "S303", "SV_RH_B_PNL_CLP", "A_ADV_AUX"
                "S303_M_SV_RH_B_PNL_CLP_A_RET_AUX"     =>         "S303", "SV_RH_B_PNL_CLP", "A_RET_AUX"
                "S303_M_SV_RH_B_PNL_CLP_C_ADV_AUX"     =>         "S303", "SV_RH_B_PNL_CLP", "C_ADV_AUX"
                "S303_M_SV_RH_B_PNL_CLP_C_RET_AUX"     =>         "S303", "SV_RH_B_PNL_CLP", "C_RET_AUX"
                "S303_M_SV_RH_B_PNL_CLP_RET_END"       =>         "S303", "SV_RH_B_PNL_CLP", "RET_END"
                "S303_M_SV_RH_B_PNL_CLP_RET_ERR"       =>         "S303", "SV_RH_B_PNL_CLP", "RET_ERR"
                "S303_M_SV_RH_B_PNL_CLP_RET_HMI_LAMP"  =>         "S303", "SV_RH_B_PNL_CLP", "RET_HMI_LAMP"
                "S303_M_SV_RH_B_PNL_CLP_RET_LAMP"      =>         "S303", "SV_RH_B_PNL_CLP", "RET_LAMP"

        - see DefinitelyActionPatterns on Semantic or appsettings.json

*)
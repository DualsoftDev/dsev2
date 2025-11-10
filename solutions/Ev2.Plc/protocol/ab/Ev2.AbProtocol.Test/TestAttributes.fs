namespace Ev2.AbProtocol.Test

open Xunit

type IntegrationFactAttribute() =
    inherit FactAttribute()

    do
        if TestHelpers.skipIntegrationTests then
            base.Skip <- "Integration tests for AB protocol are disabled (AB_SKIP_INTEGRATION=true)."

 = Get-Content -Path "Views\Planning\Plan.cshtml" -Raw

 = .IndexOf("        @{
color: var(--text-main);")
 = .IndexOf("<div id="unified-content-wrapper">", )

if ( -ge 0 -and  -gt ) {
     = .Substring(0, )
     = .Substring()
    
     = @"
        @{
            var activeTab = TempData["ActiveTab"]?.ToString() ?? "numune";
        }

        <!-- Aşama Kartları -->
        <div class="plan-tabs" style="display: none;">
            <div id="btn-tab-cps" class="plan-tab active" onclick="switchTabUnified('cps', this)">
                <i class="fa-solid fa-table-cells"></i>
                <div class="plan-tab-title">CPS PLANLAMA EKRANI</div>
            </div>
            <div id="btn-tab-calisma" class="plan-tab" onclick="switchTabUnified('calisma', this)">
                <i class="fa-solid fa-calculator"></i>
                <div class="plan-tab-title">ÇALIŞMA SAYFASI</div>
            </div>
        </div>

    "@
    
     =  +  + 
    Set-Content -Path "Views\Planning\Plan.cshtml" -Value  -Encoding UTF8
    Write-Output "Fixed Plan.cshtml successfully!"
} else {
    Write-Output "Could not find bad block or wrapper end."
}

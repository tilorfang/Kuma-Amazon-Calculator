$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    python .\make-version-resource.py
    if ($LASTEXITCODE -ne 0) { throw 'Version resource failed' }
    $exclude = @('onnxruntime.dll','onnxruntime_providers_shared.dll','vcruntime140.dll','vcruntime140_1.dll','msvcp140.dll','msvcp140_1.dll')
    $refs = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'payload') -Filter '*.dll' | Where-Object { $_.Name -notin $exclude } | ForEach-Object { '/reference:' + $_.FullName }
    $facade = Get-ChildItem C:/Windows/Microsoft.NET/assembly/GAC_MSIL/netstandard -Filter netstandard.dll -Recurse | Select-Object -First 1 -ExpandProperty FullName
    & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:winexe /platform:x64 '/out:payload/Kuma亚马逊运费计算器.exe' /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ('/reference:' + $facade) $refs '.\SingleCalculator.cs' '.\RapidOcr.cs' '.\UiControls.cs' '.\FbaModel.cs' '.\ProfitCalculator.cs' '.\Integration.cs' '.\NewUi.cs' '.\VersionInfo.cs' '.\FormulaHoverHint.cs' '.\LifecycleChecks.cs' '/win32res:version.res' /main:AmazonFreight.Program
    if ($LASTEXITCODE -ne 0) { throw 'Core build failed' }
    @'
from pathlib import Path
import hashlib,zipfile
root=Path('.')
payload=root/'payload'
entries=[]
with zipfile.ZipFile(root/'payload.zip','w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for p in sorted(payload.rglob('*')):
        if not p.is_file():continue
        name=p.relative_to(payload).as_posix()
        entries.append(hashlib.sha256(p.read_bytes()).hexdigest()+'\t'+str(p.stat().st_size)+'\t'+name)
        z.write(p,name)
(root/'payload.manifest').write_text('\n'.join(entries)+'\n',encoding='utf-8')
print('Payload:',(root/'payload.zip').stat().st_size,'bytes')
'@ | python -
    if ($LASTEXITCODE -ne 0) { throw 'Payload packaging failed' }
    & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:winexe /platform:x64 '/out:Kuma亚马逊运费计算器.exe' /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll '/resource:payload.zip,payload.zip' '/resource:payload.manifest,payload.manifest' '.\Launcher.cs' '.\VersionInfo.cs' '/win32res:version.res'
    if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed' }
} finally { Pop-Location }

param (
    [Parameter(Mandatory=$true)]
    [string]$newVersion
)

# 1. 更新 csproj 文件
$csprojPath = ".\WpfApp.csproj"
$csprojContent = Get-Content $csprojPath -Raw
$csprojContent = $csprojContent -replace '<Version>.*?</Version>', "<Version>$newVersion</Version>"
$csprojContent | Set-Content $csprojPath -Force

# 2. 更新 version.json
$versionJson = @{
    version = $newVersion
    releaseNotes = "版本 $newVersion 更新"
    downloadUrl = "https://example.com/LingYaoKeys-$newVersion.zip"
} | ConvertTo-Json

# 3. 上传到阿里云OSS
# 需要安装阿里云CLI工具
aliyun oss cp version.json oss://lingyaokeys/version.json

Write-Host "版本已更新到 $newVersion" 
#!/bin/bash

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 获取脚本所在目录的绝对路径
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR" || exit 1

# 配置
CONFIGURATION="Release"
RUNTIME="win-x64"
PLATFORM="x64"
FRAMEWORK="net8.0-windows"
BUILD_DIR="$SCRIPT_DIR/bin/${PLATFORM}/${CONFIGURATION}/${FRAMEWORK}/${RUNTIME}"
OUTPUT_DIR="$SCRIPT_DIR/output"

# 目录检查和创建函数
ensure_dir() {
    local dir="$1"
    if [ ! -d "$dir" ]; then
        mkdir -p "$dir"
        check_status "创建目录失败: $dir"
    fi
}

# 清理目录函数
clean_dir() {
    local dir="$1"
    if [ -d "$dir" ]; then
        rm -rf "${dir:?}/"*
        check_status "清理目录失败: $dir"
    else
        ensure_dir "$dir"
    fi
}

# 获取版本号
get_version() {
    local version
    version=$(grep -oP '(?<=<Version>).*?(?=</Version>)' "$SCRIPT_DIR/WpfApp.csproj")
    if [ -z "$version" ]; then
        version="1.0.0"
    fi
    echo "$version"
}

# 输出带颜色的信息
print_message() {
    local color="$1"
    local message="$2"
    echo -e "${color}${message}${NC}"
}

# 检查命令执行状态
check_status() {
    if [ $? -ne 0 ]; then
        print_message "$RED" "错误：$1"
        exit 1
    fi
}

# 检查文件是否存在
check_file() {
    local file="$1"
    local message="$2"
    if [ ! -f "$file" ]; then
        print_message "$YELLOW" "警告：$message"
        return 1
    fi
    return 0
}

# 创建压缩包函数
create_archive() {
    local source_dir="$1"
    local output_file="$2"
    
    print_message "$CYAN" "创建压缩包..."
    print_message "$CYAN" "源目录: $source_dir"
    print_message "$CYAN" "目标文件: $output_file"
    
    # 确保源目录存在
    if [ ! -d "$source_dir" ]; then
        print_message "$RED" "错误：源目录不存在: $source_dir"
        return 1
    fi
    
    # 显示源目录内容
    print_message "$CYAN" "源目录内容:"
    ls -la "$source_dir"
    
    # 转换路径格式为 Windows 格式
    # 将 /c/ 转换为 C:\，并将其他斜杠转换为反斜杠
    local win_source_dir=$(echo "$source_dir" | sed 's/^\/c\//C:\\/; s/\//\\/g')
    local win_output_file=$(echo "$output_file" | sed 's/^\/c\//C:\\/; s/\//\\/g')
    
    print_message "$CYAN" "Windows 格式路径:"
    print_message "$CYAN" "源目录: $win_source_dir"
    print_message "$CYAN" "目标文件: $win_output_file"
    
    # 使用 PowerShell 命令创建 zip 文件
    powershell -Command "
        \$ErrorActionPreference = 'Stop'
        
        \$sourcePath = '$win_source_dir'
        \$destPath = '$win_output_file'
        
        Write-Host \"Source Path: \$sourcePath\"
        Write-Host \"Destination Path: \$destPath\"
        
        # 确保源目录存在
        if (-not (Test-Path \$sourcePath)) {
            throw \"Source directory does not exist: \$sourcePath\"
        }
        
        # 确保目标目录存在
        \$destDir = Split-Path \$destPath
        if (-not (Test-Path \$destDir)) {
            New-Item -ItemType Directory -Force -Path \$destDir | Out-Null
        }
        
        # 如果目标文件已存在，则删除
        if (Test-Path \$destPath) {
            Remove-Item \$destPath -Force
        }
        
        # 创建压缩包
        Compress-Archive -Path \"\$sourcePath\*\" -DestinationPath \$destPath -Force
        
        if (-not (Test-Path \$destPath)) {
            throw \"Failed to create archive: \$destPath\"
        }
    "
    
    # 检查压缩包是否创建成功
    if [ ! -f "$output_file" ]; then
        print_message "$RED" "压缩包创建失败: $output_file"
        return 1
    fi
    
    print_message "$GREEN" "压缩包创建成功: $output_file"
    return 0
}

# 主执行流程
main() {
    # 获取版本号
    VERSION=$(get_version)
    ZIP_NAME="JX3WpfTools_v${VERSION}.zip"
    
    print_message "$GREEN" "开始发布 JX3 WPF Tools v${VERSION}..."
    print_message "$CYAN" "构建目录: $BUILD_DIR"
    print_message "$CYAN" "输出目录: $OUTPUT_DIR"
    
    # 确保输出目录存在
    ensure_dir "$OUTPUT_DIR"
    
    # 清理目录
    print_message "$CYAN" "清理目录..."
    clean_dir "$BUILD_DIR"
    rm -f "$OUTPUT_DIR/$ZIP_NAME"
    
    # 清理解决方案
    print_message "$CYAN" "清理解决方案..."
    dotnet clean -c "$CONFIGURATION"
    check_status "清理失败"
    
    # 发布项目
    print_message "$CYAN" "执行发布..."
    dotnet publish "$SCRIPT_DIR/WpfApp.csproj" \
        -c "$CONFIGURATION" \
        -r "$RUNTIME" \
        --no-self-contained \
        -p:Platform="$PLATFORM"
    check_status "发布失败"
    
    # 检查构建目录
    if [ ! -d "$BUILD_DIR" ]; then
        print_message "$RED" "错误：找不到构建目录: $BUILD_DIR"
        print_message "$YELLOW" "当前目录内容:"
        ls -la "$SCRIPT_DIR/bin"
        exit 1
    fi
    
    # 检查必要文件
    print_message "$CYAN" "检查必要文件..."
    declare -A required_files=(
        ["WpfApp.exe"]="主程序"
        ["dd/ddx64.dll"]="64位驱动"
        ["dd/ddx32.dll"]="32位驱动"
        ["AppConfig.json"]="配置文件"
        ["Resource/sound/start.mp3"]="开始音效"
        ["Resource/sound/stop.mp3"]="停止音效"
    )
    
    missing_files=0
    for file in "${!required_files[@]}"; do
        if ! check_file "$BUILD_DIR/$file" "找不到${required_files[$file]}: $file"; then
            ((missing_files++))
        fi
    done
    
    if [ $missing_files -gt 0 ]; then
        print_message "$YELLOW" "发现 $missing_files 个文件缺失"
        print_message "$YELLOW" "构建目录内容:"
        ls -la "$BUILD_DIR"
    fi
    
    # 创建压缩包
    if ! create_archive "$BUILD_DIR" "$OUTPUT_DIR/$ZIP_NAME"; then
        exit 1
    fi
    
    # 完成
    print_message "$GREEN" "发布完成！"
    print_message "$GREEN" "构建目录: $BUILD_DIR"
    print_message "$GREEN" "输出文件: $OUTPUT_DIR/$ZIP_NAME"
}

# 执行主流程
main "$@" 
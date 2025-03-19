# Development Guide

## Project Structure

```
LingYaoKeys/
├── Views/                          # View Layer
│   └── Controls/                   # Keyboard and Mouse Layout Logic
├── ViewModels/                     # ViewModel Layer
│   ├── ViewModelBase.cs           # Base ViewModel Class
│   ├── MainViewModel.cs           # Main Window ViewModel
│   ├── KeyMappingViewModel.cs     # Key Mapping ViewModel
│   ├── FloatingStatusViewModel.cs # Floating Status ViewModel
│   ├── QRCodeViewModel.cs         # QR Code ViewModel
│   ├── FeedbackViewModel.cs       # Feedback ViewModel
│   └── AboutViewModel.cs          # About ViewModel
├── Services/                       # Service Layer
│   ├── Core/                      # Core Services
│   │   ├── HotkeyService.cs        # Hotkey Service Implementation Class
│   │   ├── LyKeysService.cs      # Key Service Main Class
│   │   ├── LyKeys.cs             # Key Core Implementation Class
│   │   ├── LyKeysCode.cs         # Key Code Definition Class
│   │   ├── KeyMappingService.cs   # Key Mapping Service
│   │   └── InputMethodService.cs  # Input Method Service
│   ├── Models/                    # Service Models
│   │   ├── KeyItem.cs                # Key Item Model Class
│   │   ├── HoldKeyMode.cs           # Hold Key Mode Model Class
│   │   └── KeyModeBase.cs           # Key Mode Base Class
│   ├── Utils/                    # Utility Services
│   ├── Events/                   # Event Services
│   ├── Cache/                    # Cache Services
│   ├── Audio/                    # Audio Services
│   └── Config/                   # Configuration Services
├── Converters/                    # Value Converters
├── Behaviors/                     # Behavior Definitions
├── Styles/                        # Style Definitions
├── Resource/                      # Resource Files
└── App.xaml                       # Application Definition
```

## Development Environment

### Required Tools
- Visual Studio 2022
- .NET 8.0 SDK
- Windows Driver Kit (WDK)
- Git

### Recommended Tools
- Visual Studio Code
- Git Extensions
- Postman (API testing)
- Fiddler (network debugging)

## Development Standards

### Code Standards
- Follow C# coding standards
- Use MVVM pattern
- Define UI using XAML
- Use WPF controls
- Separate complex logic into service classes
- Use dependency injection

### Naming Conventions
- Class names: PascalCase
- Method names: PascalCase
- Variable names: camelCase
- Constant names: UPPER_CASE
- Interface names: IPascalCase
- File names: PascalCase.cs

### Comment Standards
- Class comments: Explain the purpose of the class
- Method comments: Explain parameters and return values
- Complex logic comments: Explain implementation approach
- Key code comments: Explain important logic

## Build and Run

### Development Environment
```bash
# Clone the project
git clone https://github.com/Cassianvale/LingYaoKeys.git

# Open solution
start LingYaoKeys.sln

# Run project
dotnet run
```

### Release Packaging
```bash
# Publish Release version
# Use Visual Studio's publish and packaging features
```

## Testing

### Unit Testing
- Use xUnit framework
- Test ViewModel logic
- Test Service functionality
- Test utility class methods

### Integration Testing
- Test UI interactions
- Test driver functionality
- Test performance
- Test exception handling

## Debugging

### Driver Debugging
1. Configure system to generate complete dump
2. Install debugging tools
3. Analyze using WinDbg
4. Review crash logs

### Application Debugging
1. Use Visual Studio debugger
2. Check log output
3. Analyze performance data
4. Monitor memory usage

## Contribution Guidelines

### Submitting PRs
1. Fork the project
2. Create a feature branch
3. Submit changes
4. Create a Pull Request

### Code Review
1. Follow code standards
2. Add necessary comments
3. Write unit tests
4. Update documentation

## Release Process

### Version Release
1. Update version number
2. Update changelog
3. Build release package
4. Create GitHub Release

### Documentation Updates
1. Update API documentation
2. Update user guides
3. Update development documentation
4. Update FAQs 
# Driver Overview

## Introduction

LingYaoKeys uses a kernel-level driver to implement key mapping and simulation functions. This driver is based on DeviceIoControl and provides robust anti-hook and memory protection mechanisms.

## Core Features

- **Kernel-level Implementation**: Direct access to hardware for reliable key simulation
- **Anti-Hook Protection**: Prevents third-party software from intercepting key events
- **Memory Protection**: Safeguards against memory tampering
- **Offline Operation**: Functions without internet connection
- **Device Support**: Compatible with keyboard, mouse, and joystick input
- **Performance Optimization**: Multi-threaded processing for minimal latency
- **Customization**: Support for custom scan codes and hardware features

## Architecture

The driver architecture consists of:

1. **User Mode Component**: Interfaces with the application
2. **Kernel Mode Component**: Handles low-level hardware operations
3. **Communication Layer**: Manages data transfer between user and kernel modes
4. **Security Layer**: Implements protection mechanisms

## Installation

The driver is automatically installed when you first run LingYaoKeys. Administrator privileges are required for installation. If you encounter any issues during installation, please refer to the [FAQ](/en/faq) section.

## Technical Details

For developers interested in the technical aspects of the driver, please refer to the [API Documentation](/en/driver/api) and [Status Codes](/en/driver/status-codes) sections.

## Usage Examples

For practical examples of how to use the driver functionality, see the [Examples](/en/driver/examples) section. 
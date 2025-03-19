# Status Code Documentation

## DEVICE_STATUS Enum Values

<div class="status-code-table">

| Definition                    | Description                              |
|------------------------------|------------------------------------------|
| DEVICE_STATUS_UNKNOWN (0)    | Device status unknown, initial state     |
| DEVICE_STATUS_READY (1)      | Device ready, initialization complete    |
| DEVICE_STATUS_ERROR (2)      | Device error, initialization failed      |

</div>

## Error Code Documentation

### Driver Loading Errors
<div class="error-code-table">

| Error Code | Description | Solution |
|------------|-------------|----------|
| 0x0001     | Driver file not found | Check driver file path |
| 0x0002     | Invalid driver signature | Verify driver signature |
| 0x0003     | Driver version mismatch | Update driver version |
| 0x0004     | System not supported | Check system version |

</div>

### Device Operation Errors
<div class="error-code-table">

| Error Code | Description | Solution |
|------------|-------------|----------|
| 0x0101     | Device not ready | Check device status |
| 0x0102     | Device busy | Wait for device idle |
| 0x0103     | Device not connected | Check device connection |
| 0x0104     | Device response timeout | Check device status |

</div>

### Parameter Errors
<div class="error-code-table">

| Error Code | Description | Solution |
|------------|-------------|----------|
| 0x0201     | Invalid parameter | Check parameter value |
| 0x0202     | Parameter out of range | Adjust parameter range |
| 0x0203     | Parameter type mismatch | Check parameter type |
| 0x0204     | Missing parameter | Add required parameter |

</div>

### System Errors
<div class="error-code-table">

| Error Code | Description | Solution |
|------------|-------------|----------|
| 0x0301     | Insufficient memory | Free system resources |
| 0x0302     | Insufficient system resources | Check system resources |
| 0x0303     | Insufficient privileges | Check user permissions |
| 0x0304     | System not supported | Check system requirements |

</div>

## Status Checking

### Driver Status Check
```c
DEVICE_STATUS status = GetDriverStatus();
switch (status) {
    case DEVICE_STATUS_UNKNOWN:
        // Device not initialized
        break;
    case DEVICE_STATUS_READY:
        // Device ready
        break;
    case DEVICE_STATUS_ERROR:
        // Device error
        break;
}
```

### Error Handling
```c
// Get error code
DWORD error = GetLastError();

// Handle error
if (error != 0) {
    // Error handling logic
    switch (error) {
        case 0x0001:
            // Handle driver file not found error
            break;
        case 0x0002:
            // Handle invalid driver signature error
            break;
        // ... other error handling
    }
}
```

## Debugging Tips

### Status Monitoring
- Check device status regularly
- Record status changes
- Analyze error causes

### Error Recovery
- Implement error retry mechanism
- Provide error recovery solutions
- Log error information

### Performance Optimization
- Avoid frequent status checks
- Optimize error handling process
- Set reasonable timeout values 
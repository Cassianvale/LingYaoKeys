
## windows 下使用 C# 实现驱动级键鼠模拟 

### 在Form加载时就进行热键注册


### 驱动级键鼠模拟函数
```C#
[DllImport("DD64.dll")]
static extern int DD_btn(int i);
[DllImport("DD64.dll")]
static extern int DD_mov(int x, int y);
[DllImport("DD64.dll")]
static extern int DD_key(int x, int y);
```

### DD_btn(参数)
功能： 模拟鼠标点击  
参数： 1 =左键按下 ，2 =左键放开  
4 =右键按下 ，8 =右键放开  
16 =中键按下 ，32 =中键放开  
64 =4键按下 ，128 =4键放开  
256 =5键按下 ，512 =5键放开  
例子：模拟鼠标右键 只需要连写(中间可添加延迟)  
`dd_btn(4); dd_btn(8);`

### DD_mov(int x, int y)
功能： 模拟鼠标移动  
参数： (x, y) 以屏幕左上角为原点  
例子： 把鼠标移动到分辨率1920*1080 的屏幕正中间  
`int x = 1920/2 ; int y = 1080/2;`
`DD_mov(x,y) ;`

### DD_movR(int dx, int dy)
功能： 模拟鼠标相对移动  
参数：dx, dy 以当前坐标为原点  
例子：  
把鼠标向左移动10像素  
`DD_movR(-10,0);`

### DD_whl(int whl)
功能: 模拟鼠标滚轮  
参数: 1=前, 2=后  
例子: 向前滚一格, DD_whl(1)  

### DD_key(int ddcode，int flag)
功能： 模拟键盘按键  
参数： 
- ddcode: dd驱动专用键码  
- flag，1=按下，2=放开  
例子:  
单键WIN  
`DD_key(601, 1);`
`DD_key(601, 2);`
组合键：ctrl+alt+del  
`DD_key(600,1);`
`DD_key(602,1);`
`DD_key(706,1);`
`DD_key(706,2);`
`DD_key(602,2);`
`DD_key(600,2);`

### DD_todc(参数)
功能： 转换Windows虚拟键码到dd键码  
参数： Windows虚拟键码  
例子：  
```C#
int ddcode = DD_todc(VK_ESCAPE);
Dim ddcode As int32 = DD_todc(27);
```

### DD_str(char *str)
功能： 直接输入键盘上可见字符和空格  
参数： 字符串, (注意，这个参数不是int32 类型)  
例子： DD_str("MyEmail@aa.bb.cc !@#$")  


1. 用户按下物理按键
2. WPF捕获并转换为Key枚举值
3. KeyCodeMapping将WPF Key转换为DD键码
4. 验证DD键码有效性
5. DDDriverService检查驱动状态
6. 转换为整数键码
7. 调用DD驱动的key函数
8. DD驱动执行按键操作
9. 返回执行结果
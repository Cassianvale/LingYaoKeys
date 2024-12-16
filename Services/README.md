
## windows 下使用 C# 实现驱动级键鼠模拟 

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

### DD_mov(x, y)
功能： 模拟鼠标移动  
参数： (x, y) 以屏幕左上角为原点  
例子： 把鼠标移动到分辨率1920*1080 的屏幕正中间  
`int x = 1920/2 ; int y = 1080/2;`
`DD_mov(x,y) ;`

### DD_whl(参数)
功能: 模拟鼠标滚轮  
参数: 1=前, 2=后  
例子: 向前滚一格, DD_whl(1)  

### DD_key(参数1，参数2)
功能： 模拟键盘按键  
参数： 参数1  
参数2，1=按下，2=放开  
例子： 模拟TAB按键,只需连写(中间可添加延迟)  
`DD_key(300, 1);`
`DD_key(300, 2);`

### DD_todc(参数)
功能： 转换Windows虚拟键码到dd键码  
参数： Windows虚拟键码  
例子：  
```C#
int ddcode = DD_todc(VK_ESCAPE);
Dim ddcode As int32 = DD_todc(27);
```

### DD_str(参数)
功能： 直接输入键盘上可见字符和空格  
参数： 字符串, (注意，这个参数不是int32 类型)  
例子： DD_str("MyEmail@aa.bb.cc !@#$")  
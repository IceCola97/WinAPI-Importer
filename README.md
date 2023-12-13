# WinAPI Importer

[zh-CN](/README.md) | [en-US](/README.en-US.md)

向C#程序员提供WinAPI的简单声明方式。

Provide a simple declaration method for WinAPI to C # programmers.

_(目前扩展还处于预览版)_

_(Currently, the extension is still in the preview version)_

### 用法

### Usage

1. 声明一个`Attribute`类来标记装载WinAPI函数的类，属性类的名称应该是`WindowsAPIAttribute`
2. 声明一个类，并为它加上`[WindowsAPI]`标注，例如:
```cs
[WindowsAPI]
internal static partial class WindowsAPI
{
	static WindowsAPI()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			throw new PlatformNotSupportedException(ET("当前模块仅支持在Windows上生效"));
    }
}
```
3. 在当前工程的任何位置

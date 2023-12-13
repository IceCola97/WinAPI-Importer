# WinAPI Importer

[zh-CN](./README.md) | [en-US](./README.en-US.md)

Provide a simple declaration method for WinAPI to C# programmers.

_(The extension is currently in the preview version)_

### Usage

1. Declare a `Attribute` class to mark the class that loads WinAPI functions, and the name of the attribute class should be `WindowsAPIAttribute`.
2. Declare a class and label it with a `[WindowsAPI]` annotation. The access modifier of the class will determine the access modifier of the generated function, for example:
    ```cs
    [WindowsAPI]
    internal static class WindowsNative
    {
        static WindowsNative()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException(ET("The current module only supports working on Windows"));
        }
    }
    ```
3. Type `className.functionName` anywhere in the current project, press `Alt+Enter`(quick operation), select <**Search WinAPI 'functionName'**> from the pop-up menu, and then you can preview the generated API function, as shown in the figure:
![Sample image](./images/image.png)


# WinSnake（貪食蛇）

以 **WinUI 3 / Windows App SDK** 打造的 Windows 原生桌面貪食蛇遊戲。

## 開發環境

- Windows 10 1809（`10.0.17763.0`）以上
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [winapp CLI](https://github.com/microsoft/WinAppCli) 0.7 以上

## 建置與執行

```powershell
# 由 winapp 建置、註冊偵錯身分並啟動（建議）
winapp run .

# 或使用 .NET CLI
dotnet run
```

## 封裝為 MSIX

```powershell
winapp cert generate --manifest Package.appxmanifest
winapp package .\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64 --cert .\devcert.pfx
```

## 專案結構

```text
WinSnake/
├─ Assets/               # 應用程式圖示與啟動畫面
├─ Core/                 # 與 UI 無關的遊戲引擎（後續新增）
├─ App.xaml              # 應用程式資源
├─ MainWindow.xaml       # 主視窗與標題列
├─ MainPage.xaml         # 遊戲畫面
└─ Package.appxmanifest  # MSIX 身分與視覺資產
```

---
name: quest-build
description: 一键 Quest 2 部署：切 Android 平台、Build APK、adb install + 启动。用于 P7（APK 构建）和 P8（性能优化）的快速迭代循环。可选参数 --no-launch（只装不启）、--logcat（启动后跟 logcat）。
user-invocable: true
allowed-tools:
  - Read
  - Write
  - Edit
  - Bash
  - Glob
---

# /quest-build — Quest 2 一键部署

SoccerBot 项目专用。改完代码 → 一条命令构建 APK → 推到 Quest 2 → 自动启动。

参数：`$ARGUMENTS`
- `--no-launch`：装完就退出，不启动 app
- `--logcat`：启动后跟 `adb logcat` 看 Unity 日志（性能优化时常用）

---

## 前置检查（每次都跑）

并行执行：
1. **Unity 路径**：读 `skills/quest-build/unity-path.txt`。不存在就让用户输入，输入后写入该文件。常见路径：
   - `C:/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe`
2. **adb**：`adb version`。失败就提示用户装 Android Platform Tools 并加 PATH，停。
3. **Quest 连接**：`adb devices`。没有 device 就提示「确认 Quest 已开机、USB 连接、戴上头显在弹窗里允许调试」，停。

---

## 第一次运行：自动生成 Editor 构建脚本

检查 `unity/Assets/Editor/BuildAndroid.cs` 是否存在。不存在就创建：

```csharp
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public static class BuildAndroid
{
    [MenuItem("SoccerBot/Build Android APK")]
    public static void BuildAPK()
    {
        var outDir = Path.Combine(Application.dataPath, "../../build");
        Directory.CreateDirectory(outDir);
        var apkPath = Path.Combine(outDir, "SoccerBot.apk");

        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

        var scenes = new[] { "Assets/Scenes/Main.unity" };
        var report = BuildPipeline.BuildPlayer(scenes, apkPath, BuildTarget.Android, BuildOptions.None);

        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"[BuildAndroid] OK -> {apkPath} ({report.summary.totalSize / 1024 / 1024} MB)");
        else
            EditorApplication.Exit(1);
    }
}
```

`Assets/Editor/` 不存在就先建。文件名末尾要有 `.cs.meta` 自动生成。

---

## 主流程

1. **切平台 + Build**（Unity batch mode，约 2–10 分钟）：
   ```bash
   "<UNITY_PATH>" -batchmode -quit -nographics \
     -projectPath "c:/Users/95833/Desktop/WayiProject/project/足球机器人/SoccerBot/unity" \
     -buildTarget Android \
     -executeMethod BuildAndroid.BuildAPK \
     -logFile build/unity-build.log
   ```
   `run_in_background: true`。完成后读 `unity-build.log` 末尾确认有 `[BuildAndroid] OK`。失败就把日志最后 50 行贴出来。

2. **装 APK**：
   ```bash
   adb install -r build/SoccerBot.apk
   ```
   失败常见原因：包名冲突（`-r` 已加上）、签名不一致（提示用户先 `adb uninstall com.DefaultCompany.SoccerBot`）。

3. **启动**（除非 `--no-launch`）：
   ```bash
   adb shell am start -n com.DefaultCompany.SoccerBot/com.unity3d.player.UnityPlayerActivity
   ```
   包名以 `unity/ProjectSettings/ProjectSettings.asset` 里的 `applicationIdentifier` 为准——读一下确认。

4. **logcat**（如果加了 `--logcat`）：
   ```bash
   adb logcat -s Unity SoccerBot
   ```
   `run_in_background: true`，告诉用户用 TaskOutput 或自己 Ctrl+C。

---

## 性能优化辅助（P8 用）

加 `--logcat` 后，重点看这些 tag：
- `Unity` 的 `FPS:` 输出（如果代码里加了 `Debug.Log` 帧率打印）
- `OVRPlugin` 的 `App framerate dropped`
- `IL2CPP` 的崩溃栈

如果用户问「帧率怎么样」，跑：
```bash
adb shell dumpsys SurfaceFlinger --latency com.DefaultCompany.SoccerBot/com.unity3d.player.UnityPlayerActivity
```

---

## 失败模式应对

- **Unity 路径错**：让用户重新输入，覆盖写 `unity-path.txt`
- **Build 卡住超过 15 分钟**：让用户检查 Unity 是否已开着同一个项目（batch mode 与 GUI 互斥）
- **adb 找不到 device**：提示开发者模式 + USB 调试 + 头显里允许该 PC
- **APK 装上启动后黑屏**：90% 是 XR 没启用，让用户检查 ProjectSettings → XR Plug-in Management → Android → 勾上 Oculus

不要用 `--no-verify` 或类似强制跳过的参数。失败就停下来报根因。

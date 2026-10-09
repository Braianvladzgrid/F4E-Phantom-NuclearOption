using System;
using System.IO;
using System.Security.Cryptography;
using Blueprinter;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class F4ETestBuild
{
    private static readonly string OutputRoot = Path.GetFullPath("../../../outputs/F4EPhantom");
    private static readonly string RequestPath = Path.Combine(OutputRoot, "build-test.request");
    private static bool building;
    private static double nextCheck;

    static F4ETestBuild() { EditorApplication.update += WhenReady; }

    private static void WhenReady()
    {
        if (building || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 3;
        if (File.Exists(RequestPath)) Build();
    }

    internal static bool Build()
    {
        if (building) return false;
        building = true;
        var run = Path.Combine(OutputRoot, "tests", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        var status = Path.Combine(OutputRoot, "test-build-status.txt");
        try
        {
            Directory.CreateDirectory(run);
            // Consume the request before work so errors cannot cause a build loop.
            if (File.Exists(RequestPath)) File.Move(RequestPath, Path.Combine(run, "request.txt"));
            File.WriteAllText(status, "VALIDATING\n" + run);
            if (EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("Unity has script compilation errors.");
            F4ELiveryRepair.ApplyAndValidate();
            F4ECockpitRepair.ApplyAndValidate();
            F4EIntegrationAudit.Run();
            F4ELayoutRepair.ApplyAndValidate();
            F4EExteriorUpgrade.ApplyAndValidate();
            F4EPreviewCapture.Capture();
            AssetDatabase.SaveAssets();
            OpReferenceIndex.Refresh();
            File.WriteAllText(status, "BUILDING\n" + run);
            var buildError = false;
            Application.LogCallback onLog = (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) buildError = true;
            };
            Application.logMessageReceived += onLog;
            try { ModBuilder.Build("F4EPhantom", "F-4E Phantom II", "1.1.5", run); }
            finally { Application.logMessageReceived -= onLog; }
            var package = Path.Combine(run, "F-4E Phantom II_1.1.5.nobp");
            if (buildError || !File.Exists(package) || new FileInfo(package).Length == 0)
                throw new InvalidOperationException("Build failed or produced no fresh package. See Editor.log.");
            string hash;
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(package))
                hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
            File.WriteAllText(Path.Combine(run, "package.sha256"), hash);
            File.WriteAllText(status, "READY_FOR_GAME_TEST_ONLY\n" + package + "\nSHA256=" + hash +
                "\nNot installed. Flight, weapons and loaded landing gear remain unverified.");
            Debug.Log("[F4E] Test package built and hashed: " + package + ". Not a validated release.");
            return true;
        }
        catch (Exception exception)
        {
            File.WriteAllText(status, "FAILED\n" + run + "\n" + exception);
            Debug.LogException(exception);
            return false;
        }
        finally { building = false; }
    }
}



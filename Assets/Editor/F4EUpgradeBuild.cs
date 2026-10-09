using System;
using UnityEditor;
public static class F4EUpgradeBuild
{
    public static void Run()
    {
        if(!F4ETestBuild.Build())throw new InvalidOperationException("F-4E upgrade build failed; inspect test-build-status.txt");
    }
}

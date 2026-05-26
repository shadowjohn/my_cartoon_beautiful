using System;
using System.IO;
using utility;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Sha256FileReturnsKnownHash();
            DeltreeReturnsTrueAndRemovesDirectory();
            Console.WriteLine("UtilityTests: PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static void Sha256FileReturnsKnownHash()
    {
        string file = Path.Combine(Path.GetTempPath(), "utility-sha256-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(file, "abc");
            myinclude my = new myinclude();
            string hash = my.sha256_file(file);
            AssertEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash, "sha256 hash");
        }
        finally
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    private static void DeltreeReturnsTrueAndRemovesDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "utility-deltree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "nested"));
        File.WriteAllText(Path.Combine(dir, "nested", "data.txt"), "ok");

        myinclude my = new myinclude();
        bool result = my.deltree(dir);

        AssertTrue(result, "deltree result");
        AssertFalse(Directory.Exists(dir), "deltree should remove directory");
    }

    private static void AssertTrue(bool value, string name)
    {
        if (!value)
        {
            throw new InvalidOperationException("AssertTrue failed: " + name);
        }
    }

    private static void AssertFalse(bool value, string name)
    {
        if (value)
        {
            throw new InvalidOperationException("AssertFalse failed: " + name);
        }
    }

    private static void AssertEqual(string expected, string actual, string name)
    {
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }
    }
}

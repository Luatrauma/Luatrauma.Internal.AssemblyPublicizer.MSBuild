using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Luatrauma.Internal.AssemblyPublicizer.MSBuild;

public class PublicizeAssemblies : ITask
{
  private const string EVENT_CODE_MSBUILDOUTPUTPATH_NOT_FOUND = "LTAP0001";
  private const string EVENT_CODE_DST_OUT_OF_BOUNDS = "LTAP0002";
  private const string EVENT_CODE_GENERIC_PUBLICIZE_FAILURE = "LTAP0003";
  private const string EVENT_CODE_CANT_PUBLICIZE_BAD_IMAGE = "LTAP0004";

  public IBuildEngine BuildEngine { get; set; } = null!;

  public ITaskHost? HostObject { get; set; }

  [Required]
  public string MSBuildOutputPath { get; set; } = null!;

  [Required]
  public ITaskItem[] Assemblies { get; set; } = null!;

  [Output]
  public string[]? GeneratedFiles { get; set; }

  public bool Execute()
  {
    // Make sure that we're not accidentally creating the build dir
    // at the wrong step in the MSBuild pipeline.
    if (!Directory.Exists(this.MSBuildOutputPath))
    {
      this.BuildEngine.LogErrorEvent(
        new(
          code: EVENT_CODE_MSBUILDOUTPUTPATH_NOT_FOUND,
          message: $"MSBuildOutputPath doesn't exist: {this.MSBuildOutputPath}",
          subcategory: null,
          file: null,
          lineNumber: 0,
          columnNumber: 0,
          endLineNumber: 0,
          endColumnNumber: 0,
          helpKeyword: "",
          senderName: nameof(PublicizeAssemblies)
        )
      );
      return false;
    }

    var generated = new List<string>();
    foreach (var asm in this.Assemblies)
    {
      static string NormalizeAbsolute(string path) =>
        Uri.UnescapeDataString(new Uri(Path.GetFullPath(path)).AbsolutePath);
      var src = NormalizeAbsolute(asm.ItemSpec);
      var dst = NormalizeAbsolute(asm.GetMetadata("Destination"));
      if (
        dst.LastOrDefault() == Path.DirectorySeparatorChar
        || dst.LastOrDefault() == Path.AltDirectorySeparatorChar
      )
      {
        dst = Path.Combine(dst, Path.GetFileName(src));
      }

      // Make sure we're not about to write to files outside of the build dir.
      if (!Path.GetFullPath(dst).StartsWith(Path.GetFullPath(this.MSBuildOutputPath)))
      {
        this.BuildEngine.LogErrorEvent(
          new(
            code: EVENT_CODE_DST_OUT_OF_BOUNDS,
            message: "The destination path has to stay within MSBuildOutputPath.",
            subcategory: null,
            file: null,
            lineNumber: 0,
            columnNumber: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            helpKeyword: null,
            senderName: nameof(PublicizeAssemblies)
          )
        );
        return false;
      }

      Directory.CreateDirectory(Path.GetDirectoryName(dst));

      try
      {
        BepInEx.AssemblyPublicizer.AssemblyPublicizer.Publicize(
          src,
          dst,
          new BepInEx.AssemblyPublicizer.AssemblyPublicizerOptions
          {
            Target = BepInEx.AssemblyPublicizer.PublicizeTarget.All,
          }
        );
        generated.Add(dst);
      }
      catch (BadImageFormatException)
      {
        this.BuildEngine.LogWarningEvent(
          new(
            code: EVENT_CODE_CANT_PUBLICIZE_BAD_IMAGE,
            message: $"Skipping publicization (invalid image format): {src}",
            subcategory: null,
            file: null,
            lineNumber: 0,
            columnNumber: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            helpKeyword: "",
            senderName: nameof(PublicizeAssemblies)
          )
        );
      }
      catch (Exception ex)
      {
        this.BuildEngine.LogErrorEvent(
          new(
            code: EVENT_CODE_GENERIC_PUBLICIZE_FAILURE,
            message: $"Failed to publicize assembly: {ex}",
            subcategory: null,
            file: null,
            lineNumber: 0,
            columnNumber: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            helpKeyword: "",
            senderName: nameof(PublicizeAssemblies)
          )
        );
        return false;
      }
    }
    this.GeneratedFiles = generated.ToArray();
    return true;
  }
}

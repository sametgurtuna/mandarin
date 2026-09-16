using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Mandarin.App.Resources;

/// <summary>
/// Typed access to the localized (English/Turkish) UI strings in Strings.resx /
/// Strings.tr.resx. User-facing strings should go through here rather than being
/// hardcoded in views/viewmodels, per CLAUDE.md conventions.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("Mandarin.App.Resources.Strings", Assembly.GetExecutingAssembly());

    private static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    private static string Get(string key, params object[] args) => string.Format(CultureInfo.CurrentUICulture, Get(key), args);

    public static string TrayOpen => Get("Tray_Open");
    public static string TraySettings => Get("Tray_Settings");
    public static string TrayQuit => Get("Tray_Quit");
    public static string TrayTooltip => Get("Tray_Tooltip");

    public static string SettingsTitle => Get("Settings_Title");
    public static string SettingsHeading => Get("Settings_Heading");
    public static string SettingsTagline => Get("Settings_Tagline");
    public static string SettingsLanguageLabel => Get("Settings_LanguageLabel");
    public static string SettingsRestartNotice => Get("Settings_RestartNotice");
    public static string SettingsUpdatesHeading => Get("Settings_UpdatesHeading");
    public static string SettingsCheckForUpdatesButton => Get("Settings_CheckForUpdatesButton");
    public static string SettingsUpToDateMessage(string version) => Get("Settings_UpToDateMessage", version);
    public static string SettingsExplorerHeading => Get("Settings_ExplorerHeading");
    public static string SettingsExplorerCheckbox => Get("Settings_ExplorerCheckbox");
    public static string SettingsExplorerErrorMessage(string error) => Get("Settings_ExplorerErrorMessage", error);
    public static string SettingsStartupHeading => Get("Settings_StartupHeading");
    public static string SettingsStartupCheckbox => Get("Settings_StartupCheckbox");
    public static string SettingsStartupErrorMessage(string error) => Get("Settings_StartupErrorMessage", error);

    public static string SettingsThemeLabel => Get("Settings_ThemeLabel");
    public static string SettingsThemeSystem => Get("Settings_ThemeSystem");
    public static string SettingsThemeLight => Get("Settings_ThemeLight");
    public static string SettingsThemeDark => Get("Settings_ThemeDark");

    public static string ToastConvertedTitle => Get("Toast_ConvertedTitle");
    public static string ToastConversionFailedTitle => Get("Toast_ConversionFailedTitle");
    public static string ToastMergedTitle => Get("Toast_MergedTitle");
    public static string ToastMergeFailedTitle => Get("Toast_MergeFailedTitle");
    public static string ToastPartiallyConvertedTitle => Get("Toast_PartiallyConvertedTitle");
    public static string ToastUnsupportedFileTitle => Get("Toast_UnsupportedFileTitle");
    public static string ToastUnsupportedFilesTitle => Get("Toast_UnsupportedFilesTitle");
    public static string ToastNoTargetFormatTitle => Get("Toast_NoTargetFormatTitle");
    public static string ToastNoCommonFormatTitle => Get("Toast_NoCommonFormatTitle");
    public static string ToastNoAdvancedToolsTitle => Get("Toast_NoAdvancedToolsTitle");
    public static string ToastCantMergeTitle => Get("Toast_CantMergeTitle");
    public static string ToastOpenFolderButton => Get("Toast_OpenFolderButton");

    public static string CompressTitle => Get("Compress_Title");
    public static string CompressTagline => Get("Compress_Tagline");
    public static string CompressSmallerFile => Get("Compress_SmallerFile");
    public static string CompressHigherQuality => Get("Compress_HigherQuality");
    public static string CompressQualityFormat(int quality) => Get("Compress_QualityFormat", quality);
    public static string CommonCancelButton => Get("Common_CancelButton");
    public static string CompressModeQuality => Get("Compress_ModeQuality");
    public static string CompressModeTargetSize => Get("Compress_ModeTargetSize");
    public static string CompressTargetLabel => Get("Compress_TargetLabel");
    public static string CompressCurrentSize(string megabytes) => Get("Compress_CurrentSizeFormat", megabytes);
    public static string CompressTargetInvalid => Get("Compress_TargetInvalid");
    public static string CompressTargetTooBig(string megabytes) => Get("Compress_TargetTooBigFormat", megabytes);
    public static string CompressMaxSizeLabel => Get("Compress_MaxSizeLabel");
    public static string CompressKeepOriginal => Get("Compress_KeepOriginal");
    public static string CompressLossless => Get("Compress_Lossless");
    public static string CompressResolutionLabel => Get("Compress_ResolutionLabel");
    public static string CompressAudioLabel => Get("Compress_AudioLabel");
    public static string CompressAudioAuto => Get("Compress_AudioAuto");
    public static string CompressCompressButton => Get("Compress_CompressButton");

    public static string CommonSaveButton => Get("Common_SaveButton");
    public static string CommonApplyButton => Get("Common_ApplyButton");
    public static string CommonResetButton => Get("Common_ResetButton");
    public static string CommonCloseTooltip => Get("Common_CloseTooltip");
    public static string CommonRemoveAllButton => Get("Common_RemoveAllButton");

    public static string SplitTitle => Get("Split_Title");
    public static string SplitAtLabel => Get("Split_AtLabel");
    public static string SplitButton => Get("Split_Button");
    public static string SplitFromPageLabel => Get("Split_FromPageLabel");
    public static string SplitToPageLabel => Get("Split_ToPageLabel");

    public static string CropTitle => Get("Crop_Title");
    public static string CropHint => Get("Crop_Hint");
    public static string CropAspectLabel => Get("Crop_AspectLabel");
    public static string CropAspectFree => Get("Crop_AspectFree");
    public static string CropAspectOriginal => Get("Crop_AspectOriginal");
    public static string CropSize(int width, int height) => Get("Crop_SizeFormat", width, height);
    public static string CropNoSelection => Get("Crop_NoSelection");
    public static string CropButton => Get("Crop_Button");

    public static string TrimTitle => Get("Trim_Title");
    public static string TrimHint => Get("Trim_Hint");
    public static string TrimStepBackTooltip => Get("Trim_StepBackTooltip");
    public static string TrimStepForwardTooltip => Get("Trim_StepForwardTooltip");
    public static string TrimPlayPauseTooltip => Get("Trim_PlayPauseTooltip");

    public static string RedactTitle => Get("Redact_Title");
    public static string RedactHint => Get("Redact_Hint");
    public static string RedactStyleLabel => Get("Redact_StyleLabel");
    public static string RedactStyleSolid => Get("Redact_StyleSolid");
    public static string RedactStyleBlur => Get("Redact_StyleBlur");
    public static string RedactStylePixelate => Get("Redact_StylePixelate");
    public static string RedactColorLabel => Get("Redact_ColorLabel");
    public static string RedactDetectLabel => Get("Redact_DetectLabel");
    public static string RedactDetectFaces => Get("Redact_DetectFaces");
    public static string RedactDetectText => Get("Redact_DetectText");
    public static string RedactSaveButton => Get("Redact_SaveButton");

    public static string MetadataTitle => Get("Metadata_Title");
    public static string MetadataSearchPlaceholder => Get("Metadata_SearchPlaceholder");
    public static string MetadataRemoveTooltip => Get("Metadata_RemoveTooltip");
    public static string MetadataNotice => Get("Metadata_Notice");
    public static string MetadataEmptyState => Get("Metadata_EmptyState");

    public static string EditPhotoTitle => Get("EditPhoto_Title");
    public static string EditPhotoGroupLight => Get("EditPhoto_GroupLight");
    public static string EditPhotoGroupColor => Get("EditPhoto_GroupColor");
    public static string EditPhotoGroupDetail => Get("EditPhoto_GroupDetail");
    public static string EditPhotoExposure => Get("EditPhoto_Exposure");
    public static string EditPhotoBrightness => Get("EditPhoto_Brightness");
    public static string EditPhotoContrast => Get("EditPhoto_Contrast");
    public static string EditPhotoGamma => Get("EditPhoto_Gamma");
    public static string EditPhotoSaturation => Get("EditPhoto_Saturation");
    public static string EditPhotoHue => Get("EditPhoto_Hue");
    public static string EditPhotoClarity => Get("EditPhoto_Clarity");
    public static string EditPhotoNoiseReduction => Get("EditPhoto_NoiseReduction");
    public static string EditPhotoResetTooltip => Get("EditPhoto_ResetTooltip");
    public static string EditPhotoSaveButton => Get("EditPhoto_SaveButton");

    public static string RadialModeConvert => Get("Radial_ModeConvert");
    public static string RadialModeTools => Get("Radial_ModeTools");
    public static string RadialCenterTitle => Get("Radial_CenterTitle");
    public static string RadialCenterHint => Get("Radial_CenterHint");
    public static string RadialShiftBadge => Get("Radial_ShiftBadge");
    public static string RadialAltBadge => Get("Radial_AltBadge");

    public static string FfmpegTitle => Get("Ffmpeg_Title");
    public static string FfmpegExplanation => Get("Ffmpeg_Explanation");
    public static string FfmpegSearchedLabel => Get("Ffmpeg_SearchedLabel");
    public static string FfmpegSearchedPathEnv => Get("Ffmpeg_SearchedPathEnv");
    public static string FfmpegLocateButton => Get("Ffmpeg_LocateButton");
    public static string FfmpegNotConfigured => Get("Ffmpeg_NotConfigured");
    public static string FfmpegCurrentPath(string path) => Get("Ffmpeg_CurrentPathFormat", path);
    public static string FfmpegNotValid => Get("Ffmpeg_NotValid");
    public static string FfmpegSettingsHeading => Get("Ffmpeg_SettingsHeading");
    public static string FfmpegStatusFound => Get("Ffmpeg_StatusFound");
    public static string FfmpegStatusMissing => Get("Ffmpeg_StatusMissing");
    public static string FfmpegChangeButton => Get("Ffmpeg_ChangeButton");

    public static string OutputPreview(string fileName) => Get("Output_PreviewFormat", fileName);
    public static string OutputPreviewPair(string first, string second) => Get("Output_PreviewPairFormat", first, second);

    public static string PanelTooltip => Get("Panel_Tooltip");

    public static string AdvancedToolCompress => Get("AdvancedTool_Compress");
    public static string AdvancedToolCrop => Get("AdvancedTool_Crop");
    public static string AdvancedToolTrim => Get("AdvancedTool_Trim");
    public static string AdvancedToolSplit => Get("AdvancedTool_Split");
    public static string AdvancedToolStripMetadata => Get("AdvancedTool_StripMetadata");
    public static string ToastToolFailed(string toolLabel) => Get("Toast_ToolFailedFormat", toolLabel);
    public static string ToastToolDone(string toolLabel) => Get("Toast_ToolDoneFormat", toolLabel);
}

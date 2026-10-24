using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace FlashAIOImage.Resources;

[DebuggerNonUserCode]
[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
[CompilerGenerated]
internal class Lable
{
	private static ResourceManager resourceMan;

	private static CultureInfo resourceCulture;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static ResourceManager ResourceManager
	{
		get
		{
			if (object.ReferenceEquals(resourceMan, null))
			{
				ResourceManager resourceManager = new ResourceManager("FlashAIOImage.Resources.Lable", typeof(Lable).Assembly);
				resourceMan = resourceManager;
			}
			return resourceMan;
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static CultureInfo Culture
	{
		get
		{
			return resourceCulture;
		}
		set
		{
			resourceCulture = value;
		}
	}

	internal static string APSize => ResourceManager.GetString("APSize", resourceCulture);

	internal static string APSpeed => ResourceManager.GetString("APSpeed", resourceCulture);

	internal static string btn_download_tips => ResourceManager.GetString("btn_download_tips", resourceCulture);

	internal static string Convet_Fault => ResourceManager.GetString("Convet_Fault", resourceCulture);

	internal static string Create_exe => ResourceManager.GetString("Create_exe", resourceCulture);

	internal static string Done => ResourceManager.GetString("Done", resourceCulture);

	internal static string Download_Fault => ResourceManager.GetString("Download_Fault", resourceCulture);

	internal static string Download_Image => ResourceManager.GetString("Download_Image", resourceCulture);

	internal static string Download_Image_OK => ResourceManager.GetString("Download_Image_OK", resourceCulture);

	internal static string Erase_and_program_ap => ResourceManager.GetString("Erase_and_program_ap", resourceCulture);

	internal static string Erase_Data_Fault => ResourceManager.GetString("Erase_Data_Fault", resourceCulture);

	internal static string Erase_Done => ResourceManager.GetString("Erase_Done", resourceCulture);

	internal static string Erase_Image => ResourceManager.GetString("Erase_Image", resourceCulture);

	internal static string Erase_Version => ResourceManager.GetString("Erase_Version", resourceCulture);

	internal static string FileOpen => ResourceManager.GetString("FileOpen", resourceCulture);

	internal static string ImageSize => ResourceManager.GetString("ImageSize", resourceCulture);

	internal static string ImageSpeed => ResourceManager.GetString("ImageSpeed", resourceCulture);

	internal static string Not_support_file_type => ResourceManager.GetString("Not_support_file_type", resourceCulture);

	internal static string Programing_Version => ResourceManager.GetString("Programing_Version", resourceCulture);

	internal static string setting_cancel => ResourceManager.GetString("setting_cancel", resourceCulture);

	internal static string setting_download_ap => ResourceManager.GetString("setting_download_ap", resourceCulture);

	internal static string setting_download_image => ResourceManager.GetString("setting_download_image", resourceCulture);

	internal static string setting_entry_ap => ResourceManager.GetString("setting_entry_ap", resourceCulture);

	internal static string setting_images_in_the_list => ResourceManager.GetString("setting_images_in_the_list", resourceCulture);

	internal static string setting_images_of_bin => ResourceManager.GetString("setting_images_of_bin", resourceCulture);

	internal static string setting_ok => ResourceManager.GetString("setting_ok", resourceCulture);

	internal static string setting_set_version => ResourceManager.GetString("setting_set_version", resourceCulture);

	internal static string Start_Download => ResourceManager.GetString("Start_Download", resourceCulture);

	internal static string TestLable => ResourceManager.GetString("TestLable", resourceCulture);

	internal static string TotalTime => ResourceManager.GetString("TotalTime", resourceCulture);

	internal static string USB_connected => ResourceManager.GetString("USB_connected", resourceCulture);

	internal static string USB_disconnected => ResourceManager.GetString("USB_disconnected", resourceCulture);

	internal static string Verify_AP => ResourceManager.GetString("Verify_AP", resourceCulture);

	internal static string Verify_AP_Failed => ResourceManager.GetString("Verify_AP_Failed", resourceCulture);

	internal static string Verify_Done => ResourceManager.GetString("Verify_Done", resourceCulture);

	internal static string Verify_Image => ResourceManager.GetString("Verify_Image", resourceCulture);

	internal static string Verify_Image_Failed => ResourceManager.GetString("Verify_Image_Failed", resourceCulture);

	internal static string Version => ResourceManager.GetString("Version", resourceCulture);

	internal Lable()
	{
	}
}

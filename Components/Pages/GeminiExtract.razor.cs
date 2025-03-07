using ExtractCV.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;


namespace ExtractCV.Components.Pages
{
    public partial class GeminiExtract : ComponentBase
    {
        [Inject] private CVProcessor CVProcessor { get; set; }

        private CVData cvData;

        private async Task OnFileChange(InputFileChangeEventArgs e)
        {
            try
            {
                var file = e.File;
                if (file != null)
                {
                    using var memoryStream = new MemoryStream();
                    await file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024).CopyToAsync(memoryStream);
                    var fileBytes = memoryStream.ToArray();

                    cvData = await CVProcessor.ProcessCV(fileBytes, file.Name);
                }
            }
            catch(Exception)
            {
                throw;
            }
        }
        private async Task TestOnFileChange()
        {
            Console.WriteLine("🟢 TestOnFileChange đã chạy!");
            await OnFileChange(new InputFileChangeEventArgs(null)); // Gọi thử với giá trị null
        }
    }
    public class CVFields
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Education { get; set; }
    }

    public class CVData
    {
        public CVFields Fields { get; set; }
        public string AvatarBase64 { get; set; }
    }
}

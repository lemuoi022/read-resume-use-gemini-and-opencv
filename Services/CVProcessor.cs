using System.Text.Json;
using OpenCvSharp;
using ExtractCV.Components.Pages;
using Aspose.Pdf.Devices;
using Aspose.Pdf;

namespace ExtractCV.Services
{
    public class CVProcessor
    {
        private readonly HttpClient _httpClient;
        private readonly string _geminiApiKey = "your-api-key";
        public CVProcessor(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<CVData> ProcessCV(byte[] fileBytes, string fileName)
        {
            byte[] imageBytes = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? ConvertPdfToImage(fileBytes)
                : fileBytes;

            //imageBytes = ResizeImage(imageBytes);
            var fields = await ExtractFieldsWithGemini(imageBytes);
            string avatarBase64 = ExtractAvatarWithOpenCV(imageBytes);

            return new CVData { Fields = fields, AvatarBase64 = avatarBase64 };
        }

        private byte[] ConvertPdfToImage(byte[] pdfBytes)
        {
            try
            {
                using var pdfStream = new MemoryStream(pdfBytes);
                using var document = new Document(pdfStream);
                var page = document.Pages[1];
                using var imageStream = new MemoryStream();
                var resolution = new Resolution(300);
                var jpegDevice = new JpegDevice(resolution);
                jpegDevice.Process(page, imageStream);
                return imageStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to convert PDF to image with Aspose.Pdf: " + ex.Message, ex);
            }
        }
        //private byte[] ConvertPdfToImage(byte[] pdfBytes)
        //{
        //    try
        //    {
        //        using var pdfStream = new MemoryStream(pdfBytes);
        //        using var pdfDocument = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
        //        var page = pdfDocument.Pages[0]; // Trang đầu tiên

        //        // Render thủ công với GDI+
        //        using var xGraphics = XGraphics.FromPdfPage(page);
        //        using var bitmap = new Bitmap((int)page.Width, (int)page.Height);
        //        using (var g = Graphics.FromImage(bitmap))
        //        {
        //            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        //            xGraphics.DrawImage(new XImage(page), 0, 0);
        //        }

        //        using var ms = new MemoryStream();
        //        bitmap.Save(ms, ImageFormat.Jpeg);
        //        return ms.ToArray();
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new InvalidOperationException("Failed to convert PDF to image with PdfSharp: " + ex.Message, ex);
        //    }
        //}
        //private byte[] ConvertPdfToImage(byte[] pdfBytes)
        //{
        //    try
        //    {
        //        using var pdfStream = new MemoryStream(pdfBytes);
        //        using var pdfDocument = PdfDocument.Load(pdfStream);
        //        using var image = pdfDocument.Render(0, 300, 300, true); // Render trang đầu tiên với DPI 300

        //        using var ms = new MemoryStream();
        //        image.Save(ms, ImageFormat.Jpeg);
        //        return ms.ToArray();
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new InvalidOperationException("Failed to convert PDF to image: " + ex.Message, ex);
        //    }
        //}

        //private byte[] ResizeImage(byte[] imageBytes)
        //{
        //    try
        //    {
        //        using var image = Image.Load<Rgb24>(imageBytes);
        //        image.Mutate(x => x.Resize(new ResizeOptions { Size = new SixLabors.ImageSharp.Size(800, 600), Mode = ResizeMode.Max }));
        //        using var ms = new MemoryStream();
        //        image.SaveAsJpeg(ms);
        //        return ms.ToArray();
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new InvalidOperationException("Failed to resize image: " + ex.Message, ex);
        //    }
        //}

        private async Task<CVFields> ExtractFieldsWithGemini(byte[] imageBytes)
        {
            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = "Extract Name, Email, Phone, Education from this CV image and return as JSON, and have any property is array" +
                                " change it to string" },
                                new { inline_data = new { mime_type = "image/jpeg", data = Convert.ToBase64String(imageBytes) } }
                            }
                        }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiApiKey}",
                    requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API returned {response.StatusCode}: {errorContent}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var geminiResult = JsonSerializer.Deserialize<GeminiResponse>(json);

                var rawText = geminiResult?.candidates[0].content.parts[0].text;
                var cleanedText = CleanJsonText(rawText);
                //var a = "Cleaned JSON: " + cleanedText;

                var fieldsJson = JsonSerializer.Deserialize<CVFields>(cleanedText);
                return fieldsJson ?? new CVFields { Name = "Unknown", Email = "Not found", Phone = "Not found", Education = "Not found" };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to extract fields with Gemini: " + ex.Message, ex);
            }
        }

        private string ExtractAvatarWithOpenCV(byte[] imageBytes)
        {
            try
            {
                using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
                using var gray = mat.CvtColor(ColorConversionCodes.BGR2GRAY);

                var cascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
                var faces = cascade.DetectMultiScale(gray, 1.1, 4);

                if (faces.Length > 0)
                {
                    var face = faces[0];
                    using var avatarMat = new Mat(mat, face);
                    var avatarBytes = avatarMat.ToBytes(".jpg");
                    return Convert.ToBase64String(avatarBytes);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to extract avatar with OpenCV: " + ex.Message, ex);
            }
        }

        private string CleanJsonText(string text)
        {
            var cleaned = text.Trim();
            if (cleaned.StartsWith("```"))
            {
                int firstNewLine = cleaned.IndexOf('\n');
                if (firstNewLine >= 0)
                {
                    cleaned = cleaned.Substring(firstNewLine).Trim();
                }
                if (cleaned.EndsWith("```"))
                {
                    int lastFence = cleaned.LastIndexOf("```");
                    cleaned = cleaned.Substring(0, lastFence).Trim();
                }
            }

            return cleaned;
        }
    }

    public class GeminiResponse
    {
        public Candidate[] candidates { get; set; }
    }

    public class Candidate
    {
        public Content content { get; set; }
    }

    public class Content
    {
        public Part[] parts { get; set; }
    }

    public class Part
    {
        public string text { get; set; }
    }
}

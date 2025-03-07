using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace ExtractCV.Components.Pages
{
    public partial class Test : ComponentBase
    {
        private async Task OnFileChange(InputFileChangeEventArgs e)
        {
            Console.WriteLine("Test OnFileChange triggered!");
        }
    }
}
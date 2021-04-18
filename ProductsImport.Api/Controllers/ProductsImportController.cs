using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProductsImport.Api.Data;
using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace ProductsImport.Api.Controllers
{
    [ApiController]
    [Route("products-import")]
    public class ProductsImportController : ControllerBase
    {
        private readonly string _connectionString = "CONNECTION_STRING";
        private readonly string _containerName = "container-importacao";
        private const int OneGigabyteUploadLimit = 1073741824;

        public ProductsImportController()
        {
        }

        // Endpoints

        [HttpPost]
        [DisableRequestSizeLimit]
        [RequestSizeLimit(OneGigabyteUploadLimit)]
        public async Task<IActionResult> ImportAsync(IFormFile spreadsheet, IFormFile compressedImages)
        {
            if (spreadsheet == null)
            {
                return BadRequest("Spreadsheet not found");
            }

            if (compressedImages == null)
            {
                return BadRequest("Images pack not found");
            }


            var response = await ImportFilesAsync(spreadsheet, compressedImages);

            return Ok(new
            {
                ImportId = response.ImportId,
                File = spreadsheet.FileName,
                Images = compressedImages.FileName
            });
        }

        // Private Methods

        private async Task<ImportResponse> ImportFilesAsync(IFormFile spreadsheet, IFormFile compressedImages)
        {
            // Cria um GUID pra ser a pasta da importação
            var importId = Guid.NewGuid().ToString();

            // Retorna o container de importações
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            // Faz o upload da planilha
            var spreadsheetStrem = spreadsheet.OpenReadStream();
            await containerClient.UploadBlobAsync($"{importId}/{spreadsheet.FileName}", spreadsheetStrem);
            spreadsheetStrem.Close();

            // Descomprime e faz upload das imagens

            using var compressedImagesStream = compressedImages.OpenReadStream();
            using var zipArchive = new ZipArchive(compressedImagesStream);

            for (int i = 0; i < 50; i++)
            {
                var image = zipArchive.GetEntry("shutterstock_109410320.jpg");
                if (image != null)
                {
                    using var imageStream = image.Open();
                    var response = await containerClient.UploadBlobAsync($"{importId}/{i}-{image.Name}", imageStream);
                }
            }

            // Retorna objeto de resposta

            return new ImportResponse
            {
                ImportId = importId,
            };
        }
    }
}

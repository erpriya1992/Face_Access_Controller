using FaceReader_Middleware.Models;
using FaceReader_Middleware.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

namespace FaceReader_Middleware.Controllers
{
    [ApiController]
    [Route("api/recognition-records")]
    public class RecognitionRecordsController : ControllerBase
    {
        private readonly RecognitionRecordService _service;
        private readonly RecognitionRecordDeleteService _servicedelete;
        private readonly FindFaceRecognitionRecordService _servicefindrec;
        private readonly DeleteFaceRecordsService _servicedeleteface;
        private readonly CardRecordService _servicecardrecord;

        public RecognitionRecordsController(
          RecognitionRecordService service,
          RecognitionRecordDeleteService servicedelete,
          FindFaceRecognitionRecordService servicefindrec,
          DeleteFaceRecordsService servicedeleteface,
          CardRecordService servicecardrecord)
        {
            this._service = service;
            this._servicedelete = servicedelete;
            this._servicefindrec = servicefindrec;
            this._servicedeleteface = servicedeleteface;
            this._servicecardrecord = servicecardrecord;
        }

        [HttpPost("delete")]
        public async Task<IActionResult> DeleteRecords([FromBody] RecognitionRecordDeleteRequest request)
        {
            RecognitionRecordsController recordsController = this;
            string str = await recordsController._servicedelete.DeleteAsync(request);
            return (IActionResult)recordsController.Ok((object)str);
        }

        [HttpGet("GetRecord")]
        public async Task<IActionResult> GetRecords([FromQuery] RecognitionRecordQueryRequest request)
        {
            try
            {
                string recordsAsync = await this._service.GetRecordsAsync(request);
                return this.Ok((object)recordsAsync);
            }
            catch (HttpRequestException ex)
            {
                return this.StatusCode(
                    StatusCodes.Status502BadGateway,
                    new
                    {
                        message =
                            "Cannot reach the face device HTTP API. Verify DeviceIp (or DeviceSettings:Devices:MainGate in appsettings), port (e.g. 8090), that the reader is powered on and on the same LAN, and that Windows Firewall allows outbound access. The device must expose /newFindRecords.",
                        detail = ex.Message
                    });
            }
            catch (TaskCanceledException ex)
            {
                return this.StatusCode(
                    StatusCodes.Status504GatewayTimeout,
                    new
                    {
                        message = "The face device did not respond in time. Check network latency and device load.",
                        detail = ex.Message
                    });
            }
        }

        [HttpGet("findfaceRecognition")]
        public async Task<IActionResult> FindFaceRecognition([FromQuery] FindFaceRecognitionRecordQueryRequest request)
        {
            RecognitionRecordsController recordsController = this;
            string str = await recordsController._servicefindrec.QueryAsync(request);
            return (IActionResult)recordsController.Ok((object)str);
        }

        [HttpPost("DeleteAllFaceRecordsBeforeaDate")]
        public async Task<IActionResult> DeleteBefore([FromBody] DeleteFaceRecordsRequest request)
        {
            RecognitionRecordsController recordsController = this;
            if (string.IsNullOrWhiteSpace(request.Time))
                return (IActionResult)recordsController.BadRequest((object)"Time is required in yyyy-MM-dd HH:mm:ss format.");
            string str = await recordsController._servicedeleteface.DeleteBeforeAsync(request);
            return (IActionResult)recordsController.Ok((object)str);
        }

        [HttpGet("findcardrecord")]
        public async Task<IActionResult> FindCardRecord([FromQuery] FindCardRecordQueryRequest request)
        {
            RecognitionRecordsController recordsController = this;
            string str = await recordsController._servicecardrecord.QueryAsync(request);
            return (IActionResult)recordsController.Ok((object)str);
        }
    }
}

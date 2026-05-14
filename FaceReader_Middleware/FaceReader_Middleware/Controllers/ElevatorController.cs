using System.Collections.Concurrent;
using System.Collections.Generic;
using FaceReader_Middleware.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FaceReader_Middleware.Controllers
{
    [Route("api/elevator")]
    [ApiController]
    public class ElevatorController : ControllerBase
    {
        // In-memory mapping of personId -> allowed floors.
        // In a real system this should be persisted in a database.
        private static readonly ConcurrentDictionary<string, List<int>> FloorAssignments =
            new ConcurrentDictionary<string, List<int>>();

        /// <summary>
        /// Assign allowed floors to a person. Typically called from your backend
        /// after you know which floors a visitor is allowed to access.
        /// </summary>
        [HttpPost("assign-floors")]
        public IActionResult AssignFloors([FromBody] ElevatorFloorAssignmentRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PersonId))
            {
                return BadRequest("PersonId is required.");
            }

            if (request.Floors == null || request.Floors.Count == 0)
            {
                return BadRequest("At least one floor must be assigned.");
            }

            FloorAssignments[request.PersonId] = new List<int>(request.Floors);

            return Ok(new
            {
                success = true,
                personId = request.PersonId,
                floors = request.Floors
            });
        }

        /// <summary>
        /// Get allowed floors for a person. This is what your UI can call
        /// after face recognition (you already know personId from the device).
        /// </summary>
        [HttpGet("allowed-floors")]
        public IActionResult GetAllowedFloors([FromQuery] string personId)
        {
            if (string.IsNullOrWhiteSpace(personId))
            {
                return BadRequest("PersonId is required.");
            }

            if (!FloorAssignments.TryGetValue(personId, out var floors))
            {
                return Ok(new
                {
                    success = true,
                    personId,
                    floors = new int[0]
                });
            }

            return Ok(new
            {
                success = true,
                personId,
                floors
            });
        }

        /// <summary>
        /// User selects a floor on the panel after face validation.
        /// This is where you would integrate with the elevator controller
        /// to actually open the lift door on the correct floor.
        /// Currently this just validates permission and returns success.
        /// </summary>
        [HttpPost("open-door")]
        public IActionResult OpenDoor([FromBody] ElevatorOpenDoorRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PersonId))
            {
                return BadRequest("PersonId is required.");
            }

            if (!FloorAssignments.TryGetValue(request.PersonId, out var floors) ||
                floors == null || floors.Count == 0)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "No floor permissions configured for this person.");
            }

            if (!floors.Contains(request.Floor))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Person is not allowed to access the requested floor.");
            }

            // TODO: Integrate with actual elevator control system here.
            // For now, we just simulate success.

            return Ok(new
            {
                success = true,
                personId = request.PersonId,
                floor = request.Floor,
                message = "Elevator door open command accepted for this floor."
            });
        }
    }
}


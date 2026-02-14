using BowlingApp.API.Data;
using BowlingApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BowlingApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameController : ControllerBase
    {
        private readonly BowlingContext _context;

        public GameController(BowlingContext context)
        {
            _context = context;
        }

        // POST: api/Game
        // Create a new game with players
        [HttpPost]
        public async Task<ActionResult<Game>> CreateGame([FromBody] List<string> playerNames)
        {
            // Validate input
            if (playerNames == null || playerNames.Count == 0)
            {
                return BadRequest("At least one player name is required.");
            }

            if (playerNames.Count > 4)
            {
                return BadRequest("Maximum 4 players allowed.");
            }

            // 1. Create a new Game entity
            var game = new Game
            {
                DatePlayed = DateTime.Now,
                IsFinished = false
            };

            // 2. Create Player entities for each name
            foreach (var playerName in playerNames)
            {
                var player = new Player
                {
                    Name = playerName,
                    GameId = game.Id
                };

                // 3. Initialize 10 empty Frames for each player
                for (int frameNumber = 1; frameNumber <= 10; frameNumber++)
                {
                    player.Frames.Add(new Frame
                    {
                        FrameNumber = frameNumber,
                        Roll1 = null,
                        Roll2 = null,
                        Roll3 = null,
                        Score = null
                    });
                }

                game.Players.Add(player);
            }

            // 4. Save to Database
            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            // 5. Return the created Game (reload with includes to get IDs)
            var createdGame = await _context.Games
                .Include(g => g.Players)
                    .ThenInclude(p => p.Frames)
                .FirstOrDefaultAsync(g => g.Id == game.Id);

            return CreatedAtAction(nameof(GetGame), new { id = game.Id }, createdGame);
        }

        // GET: api/Game/5
        // Get game details and current scores
        [HttpGet("{id}")]
        public async Task<ActionResult<Game>> GetGame(int id)
        {
            // 1. Find the Game by ID and include Players and Frames
            var game = await _context.Games
                .Include(g => g.Players)
                    .ThenInclude(p => p.Frames.OrderBy(f => f.FrameNumber))
                .FirstOrDefaultAsync(g => g.Id == id);

            // 2. Check if game exists
            if (game == null)
            {
                return NotFound($"Game with ID {id} not found.");
            }

            // 3. Return the Game
            return Ok(game);
        }

        // POST: api/Game/5/roll
        // Record a roll for a specific player
        [HttpPost("{gameId}/roll")]
        public async Task<IActionResult> Roll(int gameId, [FromBody] RollRequest request)
        {
            // 1. Validate input
            if (request.Pins < 0 || request.Pins > 10)
            {
                return BadRequest("Pins must be between 0 and 10.");
            }

            // 2. Find the Game and Player
            var game = await _context.Games
                .Include(g => g.Players)
                    .ThenInclude(p => p.Frames.OrderBy(f => f.FrameNumber))
                .FirstOrDefaultAsync(g => g.Id == gameId);

            if (game == null)
            {
                return NotFound($"Game with ID {gameId} not found.");
            }

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId);
            if (player == null)
            {
                return NotFound($"Player with ID {request.PlayerId} not found.");
            }

            // 3. Find the current incomplete frame
            var currentFrame = player.Frames
                .OrderBy(f => f.FrameNumber)
                .FirstOrDefault(f => !IsFrameComplete(f));

            if (currentFrame == null)
            {
                return BadRequest("Game is already complete for this player.");
            }

            // 4. Validate pins against available pins
            int availablePins = GetAvailablePins(currentFrame);
            if (request.Pins > availablePins)
            {
                return BadRequest($"Invalid roll. Only {availablePins} pins available.");
            }

            // 5. Update the current frame with the roll
            if (!currentFrame.Roll1.HasValue)
            {
                currentFrame.Roll1 = request.Pins;
            }
            else if (!currentFrame.Roll2.HasValue)
            {
                currentFrame.Roll2 = request.Pins;
            }
            else if (!currentFrame.Roll3.HasValue && currentFrame.FrameNumber == 10)
            {
                currentFrame.Roll3 = request.Pins;
            }

            // 6. Calculate scores for all frames
            CalculateScores(player.Frames.OrderBy(f => f.FrameNumber).ToList());

            // 7. Check if game is finished
            game.IsFinished = game.Players.All(p =>
                p.Frames.All(f => IsFrameComplete(f)));

            // 8. Save changes
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Roll recorded successfully",
                frame = currentFrame,
                playerFrames = player.Frames.OrderBy(f => f.FrameNumber)
            });
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        // Helper method: Check if a frame is a strike
        private bool IsStrike(Frame frame)
        {
            return frame.Roll1 == 10;
        }

        // Helper method: Check if a frame is a spare
        private bool IsSpare(Frame frame)
        {
            return frame.Roll1.HasValue && frame.Roll2.HasValue &&
                   frame.Roll1.Value + frame.Roll2.Value == 10;
        }

        // Helper method: Check if a frame is complete
        private bool IsFrameComplete(Frame frame)
        {
            // 10th frame special rules
            if (frame.FrameNumber == 10)
            {
                if (frame.Roll1 == 10) // Strike in 10th
                {
                    return frame.Roll2.HasValue && frame.Roll3.HasValue;
                }
                else if (frame.Roll1.HasValue && frame.Roll2.HasValue)
                {
                    if (frame.Roll1.Value + frame.Roll2.Value == 10) // Spare in 10th
                    {
                        return frame.Roll3.HasValue;
                    }
                    return true; // No strike or spare, only 2 rolls needed
                }
                return false;
            }

            // Regular frames 1-9
            if (frame.Roll1 == 10) // Strike
            {
                return true;
            }

            return frame.Roll2.HasValue; // Complete after 2 rolls
        }

        // Helper method: Get available pins for current roll
        private int GetAvailablePins(Frame frame)
        {
            // 10th frame special rules
            if (frame.FrameNumber == 10)
            {
                if (!frame.Roll1.HasValue) return 10;
                if (!frame.Roll2.HasValue) return frame.Roll1 == 10 ? 10 : 10 - frame.Roll1.Value;
                if (!frame.Roll3.HasValue) return frame.Roll2 == 10 ? 10 : 10 - frame.Roll2.Value;
            }

            // Regular frames
            if (!frame.Roll1.HasValue) return 10;
            if (frame.Roll1 == 10) return 0; // Strike, no second roll
            return 10 - frame.Roll1.Value;
        }

        // Helper method: Calculate scores for all frames
        private void CalculateScores(List<Frame> frames)
        {
            int cumulativeScore = 0;

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];

                if (!IsFrameComplete(frame))
                {
                    frame.Score = null; // Can't calculate yet
                    continue;
                }

                int frameScore = 0;

                if (frame.FrameNumber == 10)
                {
                    // 10th frame: just sum all rolls
                    frameScore = (frame.Roll1 ?? 0) + (frame.Roll2 ?? 0) + (frame.Roll3 ?? 0);
                }
                else if (IsStrike(frame))
                {
                    // Strike: 10 + next 2 rolls
                    frameScore = 10;

                    if (i + 1 < frames.Count)
                    {
                        var nextFrame = frames[i + 1];

                        if (nextFrame.Roll1.HasValue)
                        {
                            frameScore += nextFrame.Roll1.Value;

                            if (nextFrame.Roll2.HasValue)
                            {
                                frameScore += nextFrame.Roll2.Value;
                            }
                            else if (nextFrame.Roll1 == 10 && i + 2 < frames.Count)
                            {
                                // Two strikes in a row, need first roll of frame after next
                                var frameAfterNext = frames[i + 2];
                                if (frameAfterNext.Roll1.HasValue)
                                {
                                    frameScore += frameAfterNext.Roll1.Value;
                                }
                                else
                                {
                                    frame.Score = null; // Can't calculate yet
                                    continue;
                                }
                            }
                            else
                            {
                                frame.Score = null; // Can't calculate yet
                                continue;
                            }
                        }
                        else
                        {
                            frame.Score = null; // Can't calculate yet
                            continue;
                        }
                    }
                }
                else if (IsSpare(frame))
                {
                    // Spare: 10 + next 1 roll
                    frameScore = 10;

                    if (i + 1 < frames.Count)
                    {
                        var nextFrame = frames[i + 1];
                        if (nextFrame.Roll1.HasValue)
                        {
                            frameScore += nextFrame.Roll1.Value;
                        }
                        else
                        {
                            frame.Score = null; // Can't calculate yet
                            continue;
                        }
                    }
                }
                else
                {
                    // Open frame: just sum the rolls
                    frameScore = (frame.Roll1 ?? 0) + (frame.Roll2 ?? 0);
                }

                cumulativeScore += frameScore;
                frame.Score = cumulativeScore;
            }
        }
    }

    public class RollRequest
    {
        public int PlayerId { get; set; }
        public int Pins { get; set; }
    }
}
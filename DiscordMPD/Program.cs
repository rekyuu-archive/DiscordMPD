using System;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using DiscordRPC.Logging;
using MpcCore;
using MpcCore.Contracts;
using MpcCore.Contracts.Mpd;

namespace DiscordMPD
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            // Set up MPD client.
            MpcCoreConnection mpcConn = new();
            MpcCoreClient mpcClient = new(mpcConn);

            await mpcClient.ConnectAsync();
            
            // Set up Discord client.
            DiscordRpcClient discordClient = new DiscordRpcClient("880327448900821033");

            discordClient.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
            discordClient.Initialize();

            // Data used to determine if a presence update is needed.
            string currentSong = "";
            string currentAlbum = "";
            DateTime currentSongStartTime = DateTime.UtcNow;
            while (true)
            {
                Thread.Sleep(1000);
                
                // Grab current status information.
                IMpcCoreResponse<IStatus> status = await mpcClient.SendAsync(new MpcCore.Commands.Status.GetStatus());

                // If music isn't currently playing (paused, stopped, etc.) disable the presence.
                if (!status.Result.IsPlaying)
                {
                    if (discordClient.CurrentPresence != null) discordClient.ClearPresence();
                    continue;
                }

                // Get current playing song information.
                IMpcCoreResponse<IItem> nowPlaying = await mpcClient.SendAsync(new MpcCore.Commands.Status.GetCurrentSong());
                string song = nowPlaying.Result.Title;
                string album = nowPlaying.Result.Album;
                DateTime songStartTime = DateTime.UtcNow.AddSeconds(-status.Result.Elapsed);

                // If the current song and album hasn't changed, and the presence is active,
                // don't update anything. Only update the timestamp if the user has fast-forwarded or rewound.
                if (currentSong == song && currentAlbum == album && discordClient.CurrentPresence != null)
                {
                    if (Math.Abs((currentSongStartTime - songStartTime).Seconds) > 1)
                    {
                        discordClient.UpdateStartTime(songStartTime);
                        currentSongStartTime = songStartTime;
                    }
                    continue;
                }
            
                // Set the new presence with current song information.
                discordClient.SetPresence(new RichPresence()
                {
                    Details = song,
                    State = album,
                    Timestamps = new Timestamps(songStartTime)
                });

                currentSong = song;
                currentAlbum = album;
                currentSongStartTime = songStartTime;
            }
        }
    }
}
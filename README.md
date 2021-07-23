# SpotifyTrackInfo_OBS
Simple console application that uses the Spotify web API to display song information on a local webpage (for software like OBS).

# Usage
- Compile/Download the latest release from the [releases page](https://github.com/LoghamLogan/SpotifyTrackInfo_OBS/releases).
- Run the application.
- Spotify will ask you to confirm permissions (via the broswer) for the application "Spotify Track Information for OBS" to access your song information.
- The application will serve a local webpage, and keep it updated with track information. By default this is located at: http://localhost:8080/trackinfo/

![webpage pic](https://raw.githubusercontent.com/LoghamLogan/SpotifyTrackInfo_OBS/master/screenshots/trackinfopage.png)
- In OBS (or your prefered streaming application) add a browser source pointing to this webpage: (http://localhost:8080/trackinfo/).

# Application Screenshot
![running pic](https://raw.githubusercontent.com/LoghamLogan/SpotifyTrackInfo_OBS/master/screenshots/running.png)

# Notes
- You can edit how the track information is displayed by editing the TrackInfo.html file in the pages folder.
- The information is updated every 6 seconds. You can change this value, but will risk being throttled by Spotify. See [Rate Limiting](https://developer.spotify.com/web-api/user-guide/#rate-limiting) section of their user guide.

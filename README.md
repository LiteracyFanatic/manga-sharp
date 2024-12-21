# MangaSharp

## About

MangaSharp is a CLI that supports downloading manga and other comics from a variety of sites. All images will be converted to the WebP format to optimize disk usage while still maintaining high quality. It also includes a lightweight web interface for reading locally saved manga. Give it a try if you like the sound of fullscreen images on a black background with minimal distracting UI elements.

## Usage

- Call `manga download URL` with the URL for the manga's index or chapter page (e.g. https://mangadex.org/title/dd8a907a-3850-4f95-ba03-ba201a8399e3/fullmetal-alchemist). You can pass multiple URLs at once if you would like. By default, the reading direction will be assumed to be horizontal but this can be changed with `--direction`. If you are downloading a manhwa for example, you will want to pass `--direction vertical` before the URL. `--direction` can be specified multiple times and will affect all URLs following it.
- Call `manga update TITLE` with the title of a previously downloaded manga to check for new chapters. Use `--all` to check all downloaded titles for updates.
- `manga ls` will list information about the manga in you collection. Use `--json` if you need structured output.
- `manga rm TITLE` will remove a downloaded manga from the database and delete all associated images from disk. `--all` will delete all downloaded manga.
- `manga archive TITLE` is similar to `manga rm` but only deletes images from disk while leaving the database entries intact. This lets you free up space without losing your reading history. `--all` can be used to archive all manga. `--from-chapter` and `to-chapter` can be used with chapter titles to only archive part of a manga. They will default to the first and last chapters if omitted. `--from-chapter` and `--to-chapter` can not be combined with `--all`.
- `manga unarchive TITLE` restores a previously archived manga. This only changes the status in the database, so you will need to run `manga update` afterwards to actually download the content. `--all` can be used to restore all archived manga. `--from-chapter` and `to-chapter` can be used with chapter titles to only restore part of a manga. They will default to the first and last chapters if omitted. `--from-chapter` and `--to-chapter` can not be combined with `--all`.
- `manga read` will start a server and open a web page with a list of your downloaded manga in your default browser. `--no-open` will just start the server and prevent the browser from opening automatically. `--title` and `--last` allow you to skip the index page and jump straight to reading the corresponding manga. Use `--port` to set the port the server will listen on.

## Install

### Download an executable

Just download the appropriate executable for your operating system from the [releases](https://github.com/LiteracyFanatic/manga-sharp/releases). You will also need to have the [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) installed. This is the recommended installation strategy, but self-contained versions of the executables are also provided for convenience. Note that these are much larger since they bundle a copy of the runtime with the executable.

### AUR

For Arch Linux users, an AUR package is available at https://aur.archlinux.org/packages/manga-sharp.

```bash
yay -S manga-sharp
```

## Develop

Running `./build` will install necessary assets from npm and copy them to `wwwroot` and then build the project. Then you can use `dotnet run` or `dotnet watch` as usual. Setting `DOTNET_ENVIRONMENT=Development` will enable more verbose logging.

## Release

`./publish` will build standard and self-contained versions of the project for both Windows and Linux. It also generates a `checksums.txt` file.

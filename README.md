<p align="center">
  <img alt="Typedown Logo" src="./logo.png" width="100px" />
  <h1 align="center">Typedown</h1>
</p>

[![Typedown Download](https://get.microsoft.com/images/en-us%20light.svg)](https://apps.microsoft.com/detail/9p8tcw4h2hb4)

Typedown is a lightweight Markdown editor designed specifically for the Windows platform. With the WinUI framework, it provides users with a seamless interface and efficient editing experience that perfectly matches the operating system. Whether you're writing technical documents, academic papers, or blog posts, Typedown is your go-to assistant!

## Screenshots
<figure>
<img src="https://github.com/byxiaozhi/Typedown/assets/31278216/d0c9d76b-ecd2-4941-90ca-0f8c639c2ef0" width=200/>
<img src="https://github.com/byxiaozhi/Typedown/assets/31278216/d5320590-2d0b-4f9a-a3d2-4661eb021758" width=200/>
<img src="https://github.com/byxiaozhi/Typedown/assets/31278216/2ce7795c-1043-41ed-a420-c42f7aa5aa80" width=200/>
<img src="https://github.com/byxiaozhi/Typedown/assets/31278216/a2df17f3-3100-4129-b0ba-0e90e14a89bf" width=200/>
</figure>

## Building from source

### 1. Prerequisites
[Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the following individual components:
  - .NET Core 3.1 SDK
  - Git for Windows

[Node.js](https://nodejs.org/) with the following global packages:
  - [yarn](https://yarnpkg.com/)

### 2. Clone the repository
```ps
git clone https://github.com/byxiaozhi/Typedown
```

This will create a local copy of the repository.

### 3. Build the project
First go to the directory `Typedown\Dev\Typedown.Editor` and run `yarn && yarn build`

```ps
cd Typedown\Dev\Typedown.Editor
yarn && yarn build
```
![20240319232236_rec_](https://github.com/byxiaozhi/Typedown/assets/31278216/3f038707-9311-4aad-846b-a22e8bad6857)

After finishing the compilation of `Typedown.Editor`, you can see the generated product in the directory `Typedown\Dev\Typedown\Resources\Statics`.

Then use VisualStudio 2022 to open `Typedown\Typedown.sln`, right-click on the Typedown project and select Set as Startup Project.

In the top pane, select the solution configuration you want to build in, the difference between these configurations is as follows
- Debug: The `Typedown.Editor` will be accessed using the http://localhost:3000 address, to use this configuration you need to also start the Typedown.Editor project using yarn start in the Typedown\Dev\Typedown.Editor directory.
- Debug_Local: The `Typedown.Editor` will be accessed using the compiled product (Typedown\Dev\Typedown\Resources\Statics)
- Release: Used when releasing a project

Then select the platform you want to build on (x64, x86, or arm64) and click Run!

![20240319232529_rec_](https://github.com/byxiaozhi/Typedown/assets/31278216/50ef6e56-b177-49b0-b361-83659d25a40e)

### Contributors
Want to contribute to this project? Let us know with an [issue](https://github.com/byxiaozhi/Typedown/issues) that communicates your intent to create a [pull request](https://github.com/byxiaozhi/Typedown/pulls).

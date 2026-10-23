# Windows port

The native Windows WPF application adapts Hang Later's MIT-licensed macOS interaction and interface, including the Done Spike geometry, receipt mesh and gesture parameters from `Sources/HangLater/ReceiptSpike3D.swift`, the completed archive from `MainView.swift` / `HangLaterCore/CompletionArchive.swift`, and the settings and source-dialog layouts. Copyright (c) 2026 Yuxuan Fu; see `LICENSE`. Windows uses WPF diffuse/specular lighting and Win32 APIs, so reflections and text rasterization differ from SceneKit/AppKit. Original theme artwork is retained in `Windows/HangLater/Resources`.

The portable Windows EXE includes the .NET runtime. .NET is MIT-licensed; runtime third-party notices are maintained by the .NET Foundation at https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT.

# Fluent UI System Icons

The Windows Done Spike document-text icon is rasterized from the unmodified `ic_fluent_document_text_24_filled.svg` in https://github.com/microsoft/fluentui-system-icons.

MIT License

Copyright (c) Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

# Tendedero

https://github.com/alejandrobujan/tendedero

DesktopPanel, the rope geometry and the Carbon hotkey registration are adapted from or informed by the following MIT-licensed source files: LinePanel.swift, LineView.swift and HotKey.swift. The Tendedero name, icon and artwork are not reused.

MIT License

Copyright (c) 2026 Alejandro Buján

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

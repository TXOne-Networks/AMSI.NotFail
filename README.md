# PSHack — Minimal PowerShell Runspace Demo
A tiny C# console app that embeds PowerShell in-process.
It creates a Runspace, sets it as Runspace.DefaultRunspace, builds a ScriptBlock with a param, invokes it, and prints the results.
Public 

👉 DEMO Site: [http://amsinot.fail/](http://amsinot.fail/)

## Features
- Create & open a PowerShell Runspace
- Set Runspace.DefaultRunspace for downstream APIs
- Build a ScriptBlock with param() and invoke with arguments
- Collect and print results

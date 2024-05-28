## Scarlett

### TL;DR

Scarlett is a hackable voice assistant for Windows that allows to perform various 
actions on recognized phrases with the grammar "[assistant] verb noun", e.g. "wake computer".
The set of phrases and the corresponding actions is specified in the hierarchical config. 
The actions include execution of external programs, HTTP requests, Wake on LAN, etc. 
Users can create their own actions either by writing them in C# as a part of the
application or by executing as external scripts.

<hr>

Because Windows speech recognition works surprisingly well with inexpensive microphone
arrays such as PS3 Eye and has very handy API, it is tempting to use it in a voice
assistant that can do things which many smarter off-the-shelf assistants can not.
For example, with Scarlett you can control devices of your own make, simulate user
input and send HTTP requests. Although currently it only listens and does not talk
back. Essentially, Scarlett is a yet another ubiquitous command interface such as 
[Enso](https://gchristensen.github.io/enso-portable/) or 
[Ubiquity](https://gchristensen.github.io/ishell/).

### Scarlett Actions

Currently, Scarlett supports the following set of actions:

* **run** - runs the specified program with the given arguments.
* **shell-open** - opens the specified file in the associated program.
* **url** - opens the specified URL in the default browser.
* **wol** - sends magic packets to wake up a network host.
* **http** - performs a HTTP request.
* **sony-ircc** - rends a HTTP IRCC command to an IP-controlled Sony TV.
* **serial** - sends a serial command to a connected device, e.g. Arduino.
* **simulate-input** - simulates user input with SendKeys.SendWait.
* **close-window** - close the currently active window.

### Recognized Phrases

Phrases that Scarlett could recognize and their corresponding actions are specified 
in a hierarchical JSON config. For example, the following config defines 
two actions: "open browser" and "open slideshow".

```json
{
  "actions": {
    "open": {
      "browser": {
        "action": "run",
        ...
      },
      "slideshow": {
        "action": "shell-open",
        ...
      }
    }
  }
}

```

For more details please see the [sample config](https://github.com/GChristensen/scarlett/blob/main/settings.json) 
which should be self-explanatory.

### Creating Your Own Actions

You can create the logic of your own actions either in a scripting language you can execute
with the "run" action, or by copying the [action template](https://github.com/GChristensen/scarlett/blob/main/actions/user/TemplateAction.cs)
inside `Scarlett.actions.user` 
namespace and rebuilding the application.
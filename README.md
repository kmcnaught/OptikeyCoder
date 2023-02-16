# OptiKey Coder

This is an experimental fork of [OptiKey](https://github.com/OptiKey/OptiKey) with some features and ideas specific to users who want to write code. 

There is a low bar to adding features/hacks in the interest of trialling different workflows and seeing what is most helpful. However, the stability this fork is not guaranteed - if you like a feature, open an issue to discuss whether it's appropriate for going back into the core Optikey app. 

Core features we have experimented with so far:
- Function keys to quickly switch between Presage prediction for prose (comments etc) and basic prediction for syntax
- Swap between small language-specific dictionaries for syntax word prediction
- Add words to dictionary temporarily (for this session) or permanently
- Add words from clipboard to train Presage
- Allow trigger gamebutton button to be held down for repeat presses
- A plugin to copy text to clipboard via a regex filter
- Add larger SuggestionGrid for situations in which you want more suggestions visible

Keyboard features:
- Reset prediction mid-word for camel_case, snakeCase variable names 
- Specific keyboard for VS Code, including lockable shortcut bar, snippets, navigation and more
- Colour styling of keyboard to help find symbols


## Main VS Code keyboard interface
![Screenshot of Optikey Coder with main VS Code keyboard loaded](https://user-images.githubusercontent.com/12151404/219380689-17925bfa-0005-44d5-907c-651d08702dc9.PNG)

## Navigation/selection keys for VS Code
![Screenshot of Optikey Coder with VS Code's navigation keyboard loaded](https://user-images.githubusercontent.com/12151404/219380685-81b8d400-5cfb-4f45-80a4-67ed7a8ec84f.PNG)

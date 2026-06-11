# Workshop 

Initialize a new TypeSpec project with the following command:



.gitattributes
```gitattributes
# Normalize line endings solution-wide: text is stored as LF and checked out
# as LF on every OS, except where a format/tool requires otherwise.
* text=auto eol=lf

# Must be CRLF (multipart/form-data spec + Windows tooling)
*.http  text eol=crlf
*.sln   text eol=crlf
*.cmd   text eol=crlf
*.bat   text eol=crlf

# Must be LF (break on Linux otherwise)
*.sh    text eol=lf

# Binary: never touch line endings
*.pdf   binary
*.png   binary
*.jpg   binary
*.jpeg  binary
*.gif   binary
*.ico   binary
*.zip   binary
*.dll   binary
*.exe   binary
```
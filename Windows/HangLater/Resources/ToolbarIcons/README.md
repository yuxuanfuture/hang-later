# Desktop clothesline toolbar icon

The two JPEGs are exact copies of the user's references:

- `dark/desktop-clothesline.jpg`: image 1, message ID 1120977146456410465.
- `light/desktop-clothesline.jpg`: image 2, message ID 3562340475250212107.

`ToolbarIconImages.DesktopClothesline` interprets each original black-matted
JPEG as a premultiplied-alpha display texture. This avoids a black rectangle
on the Light header; it does not redraw the glyph or rewrite the source files.
The two source color variants are kept separate. The main header uses an
18 × 16 DIP image, and the existing button click handler is unchanged.

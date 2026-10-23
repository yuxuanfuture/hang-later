# Windows resource provenance

The application uses Hang Later's paper texture, clothesline clips, empty-state leaf, pin artwork and theme-specific UI PNG/JPEG exports. These assets are retained as source resources under `HangLater/Resources`, using the same Light/Dark mapping as the interface. Hang-something icons use the supplied @2x exports at logical control sizes; obsolete @1x duplicates and the superseded pre-rendered spike turntable are not shipped. The spike itself is rendered interactively by WPF, not a sequence of screenshots.

The document-text icon is rasterized from Microsoft's MIT-licensed Fluent UI System Icons. See the repository's `THIRD_PARTY_NOTICES.md`. Artwork is not recolored or regenerated during this source cleanup. Reference screenshots, test captures, private task backups and development binaries are not distribution assets.

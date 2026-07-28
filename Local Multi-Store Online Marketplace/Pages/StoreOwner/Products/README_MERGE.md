# Products merge — drop-in notes

This folder replaces your existing `Pages/StoreOwner/Products/` folder **and**
absorbs `Pages/StoreOwner/Explore/` into it. Everything now lives under one
"Products" title with three tabs: **Catalog**, **Promote**, **Boost**.

## How to install

1. Delete `Pages/StoreOwner/Products/` and `Pages/StoreOwner/Explore/` from your project.
2. Copy this whole `Products/` folder into `Pages/StoreOwner/`.
3. Remove any sidebar/nav link that pointed at `/StoreOwner/Explore/Index` and
   point it at `/StoreOwner/Products/Index` instead (or delete it — the new
   Catalog page's tab strip now links to Promote directly).
4. Rebuild. If your IDE cached the old `Explore` namespace anywhere (e.g. a
   stray `_ViewImports.cshtml` `@using`), remove that reference too.

## What actually changed

**Folder layout**
```
Products/
  Index.cshtml(.cs)         Catalog — unchanged logic, now has the tab strip
  Create.cshtml(.cs)        unchanged logic, now uses the shared validator
  Edit.cshtml(.cs)          unchanged logic, now uses the shared validator
  Delete.cshtml(.cs)        unchanged
  Boost.cshtml(.cs)         unchanged logic, now has the tab strip
  _CategoryOption.cshtml(.cs) unchanged
  _ProductsNav.cshtml       NEW — shared Catalog/Promote/Boost tab strip
  Shared/
    ProductMediaValidator.cs NEW — extracted validation, see below
  Promote/                  was Explore/, renamed
    Index.cshtml(.cs)       was Explore/Index — same behavior
    Create.cshtml(.cs)      was Explore/Create — same behavior
    Edit.cshtml(.cs)        was Explore/Edit (an empty stub already)
```

**Namespace rename**
`Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Explore` →
`Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products.Promote`

**Route rename**
`/StoreOwner/Explore/*` → `/StoreOwner/Products/Promote/*` (all internal
`RedirectToPage` / `asp-page` references updated to match).

**Visible rebrand**
Page titles/headers "Explore Content" → "Promote" and "Create Explore Post" →
"Create Promote Post". Everything else (post types, captions, likes/comments,
linked product, etc.) is untouched — it's still the same feature underneath.

**New shared tab strip (`_ProductsNav.cshtml`)**
Included at the top of Catalog's Index, Promote's Index, and Boost, so a
store owner always sees Catalog / Promote / Boost as one section instead of
two disconnected sidebar links. Boost has no list page of its own (it's
launched per-product from a Catalog card), so its tab is only ever "active"
on the Boost page itself — it's not a dead link elsewhere.

**Extracted duplication — `Shared/ProductMediaValidator.cs`**
Before the merge:
- `Products/Create.cshtml.cs` and `Products/Edit.cshtml.cs` each had their own
  byte-for-byte copy of image extension/MIME/magic-byte validation (the old
  code even had a comment flagging this as "worth extracting").
- `Explore/Create.cshtml.cs` (now `Promote/Create.cshtml.cs`) had a third,
  slightly different copy that validated images *and* video, but — unlike
  the Products pages — never checked the actual file signature (magic
  bytes), only the extension.

Now there's one static class, `ProductMediaValidator`, with:
- `ValidateImageBasics(file, maxSizeBytes)` — extension/MIME/size
- `ValidateVideoBasics(file, maxSizeBytes)` — extension/size
- `HasValidImageSignatureAsync(file)` — magic-byte check

All three pages call into it with their own size/count limits (Products:
5 MB/image, 5 images; Promote: 8 MB/image, 25 MB/video). One side effect
worth knowing about: **Promote's image validation is now slightly stricter
than before** — it runs the same magic-byte check Products always used, so a
renamed non-image file that used to pass Explore's checks will now be
rejected there too. That's a bug fix, not a behavior regression, but flagging
it since it does change what Promote accepts.

## What did *not* change

- All business logic (subscription checks, boost payment/Stripe flow, order
  history archive-vs-delete rules, wishlist/cart/review cleanup on delete,
  ownership checks, etc.) is untouched — copied as-is.
- Physical upload storage paths are untouched: product images still save to
  `/uploads/products/{id}/`, and Promote posts still save to
  `/uploads/explore/{id}/`. I didn't rename the storage folder to avoid
  needing a data migration for anything already uploaded; only the *URL
  routes and C# namespace* changed, not where files live on disk.
- Database table/entity names (`ExplorePost`, `ExploreMedia`, etc.) are
  untouched — renaming those would mean an EF Core migration, which is a
  bigger, separate decision.

## One thing to double check yourself

I don't have your `_StoreOwnerLayout.cshtml` or sidebar partial in these two
zips, so I couldn't update whatever nav link currently points at
`/StoreOwner/Explore/Index` — you'll need to repoint or remove that one link
manually (step 3 above).

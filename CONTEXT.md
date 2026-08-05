# Aether Garden

The personal site of Duck Ran. This context covers the backend serving content (posts, notes), GitHub overview, profile, and music (Apple Music favorites and link conversion to Netease).

## Content

**Post**:
A long-form markdown entry served under `/api/blog`.
_Avoid_: article, blog entry, content item

**Note**:
A short-form markdown entry served under `/api/notes`, sharing the Post shape.
_Avoid_: snippet, short post

**Slug**:
The URL-safe, case-insensitive identifier of a Post or Note, unique per kind.
_Avoid_: id, key

**Excerpt**:
The short plain-text summary shown in list views, taken from front matter or derived from the body.
_Avoid_: summary, teaser

**Front matter**:
The YAML metadata block at the top of a content file, holding slug, title, date, tags, excerpt, and status.
_Avoid_: header, metadata block

**Status**:
A content file's publication state; only files marked `published` are served.
_Avoid_: state, flag

**Paragraph**:
A plain-text block derived from rendered markdown; the list a PostDetail exposes as its content.
_Avoid_: content block, section

## Music

**Track**:
A single song exposed by the music module, pairing an Apple Music URL with a Netease URL.
_Avoid_: song, item

**Netease link**:
The 网易云 (163.com) URL paired with a Track's Apple Music URL; absent when the Netease search fails.
_Avoid_: conversion, netease url

**Converter**:
The feature that turns an Apple Music song URL into a Track carrying both links.
_Avoid_: link service, song resolver

## External data

**Fallback**:
Data returned when a live source is unreachable, explicitly marked so callers know it is not live.
_Avoid_: mock, stub, placeholder

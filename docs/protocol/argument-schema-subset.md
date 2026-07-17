# DearStory argument schema subset

DearStory uses a constrained JSON Schema Draft 2020-12 subset for story arguments. The canonical machine-readable definition lives at `schemas/arguments/dearstory-args.schema.json`.

## Supported keywords

The subset accepts only these keywords:

- `type`
- `properties`
- `required`
- `enum`
- `minimum`
- `maximum`
- `minLength`
- `maxLength`
- `items`
- `default`
- `x-dearstory-control`
- `x-dearstory-order`
- `x-dearstory-category`
- `x-dearstory-visible`

Any other keyword is rejected with the stable diagnostic code `args.unsupported_keyword`.

## Supported types

`type` may be one of:

- `object`
- `boolean`
- `integer`
- `number`
- `string`
- `array`

## Patch semantics

DearStory applies patches as JSON Merge Patch style updates:

- object patches merge recursively into the current argument object;
- non-object patches replace the current value;
- a rejected patch preserves the previous argument snapshot;
- an accepted patch returns the merged snapshot.

## Validation rules

- `required` is evaluated against object properties.
- `enum` requires an exact JSON value match.
- `minimum` and `maximum` apply to numeric values.
- `minLength` and `maxLength` apply to string values.
- `items` validates each array element against the nested schema.
- `default` and the `x-dearstory-*` annotations carry metadata only and do not change validation outcomes.

## Diagnostic codes

Current validators emit these stable codes:

- `args.unsupported_keyword`
- `args.type`
- `args.required`
- `args.enum`
- `args.minimum`
- `args.maximum`
- `args.min_length`
- `args.max_length`

Both the native and managed validators must emit the same codes for the same vector inputs.

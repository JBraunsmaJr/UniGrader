# Question / Answer Mode

By leveraging the following configuration you can
create a wide-variety of quiz-like projects!

The current implementation requires the submitted projects output their answers as JSON text to stdout. Whether that's via `print` `Console.WriteLine` `System.out`, etc... This output is then processed! The JSON output must be their
**FINAL** output 

### TODO
- Adding support for a project to generate json file instead of outputting to stdout

## answerkey.json
When using the Q/A framework you are required to provide an answer key.

### Question Object
| Property  | Value Type(s) | Description |
|-----------| --- | --- |
| points    | number, dict(full, partial) | Indicates the number of points you can receive if answered correctly |
| matchType | "any", "all", "exact" (default is any) | Optional property, determines the behavior of how to check an array | 
| expected  | str, number, array, expectedObjectDict | Multiple options available for determining an answer |

## Example
```json
{
  "questionID": {
    "points": 3,
    "expected": 64
  },
  
  "questionID": {
    "points": 10,
    "matchType": "any",
    "expected": ["valid answer", "is", "any", "of", "these", "entries"]
  },
  
  "questionID": {
    "points": {"full": 10, "partial": 2},
    "expected": {
      "full": "valid answer",
      "partial": "partially right answer"
    }
  },
  
  "questionID": {
    "points": {"full": 10, "partial": 2},
    "expected": {
      "full": 10,
      "partial": {
        "matchType": "any",
        "expected": [1,2,3,4,5,6]
      }
    }
  }
}
```

## Output
**Note**: the grade is a percentage value between 0-1
Total points indicates total possible points

```json
{
  "unique_name": {
    "Grade": "0.xx",
    "Wrong": {
      "questionID": "their answer"
    },
    "Points": "x.x",
    "TotalPoints": "x.x"
  }
}
```

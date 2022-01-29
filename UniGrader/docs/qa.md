# Question / Answer Mode

By leveraging the following configuration you can
create a wide-variety of quiz-like jams!

## key.json
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
```json
{
  "team_id": {
    "grade": "0.xx",
    "wrong": {
      "questionID": "their answer"
    },
    "points": "x.x"
  }
}
```
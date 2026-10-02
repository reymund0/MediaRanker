## ADDED Requirements

### Requirement: IMDb TV load keeps only numbered seasons
The IMDb load SHALL write season numbers on season collections and episode numbers on episode media, including when updating existing rows. It SHALL NOT create seasons or episodes for IMDb rows without a season number. After loading episodes it SHALL, in bounded batches, delete IMDb episodes in unnumbered seasons, unnumbered seasons left without episodes, and IMDb series left without seasons by this step. It SHALL NOT delete any title or series that has a review, and SHALL log the number deleted and skipped per batch.

#### Scenario: New unknown-season episode in the feed
- **WHEN** the episode feed contains an episode with season number -1
- **THEN** the load creates no season or episode for it

#### Scenario: Existing unknown-season data
- **WHEN** the load runs on a catalog with an `Unknown` season holding unreviewed episodes
- **THEN** those episodes and the season are deleted, and a series whose only season it was is deleted too

#### Scenario: Reviewed episode in an unknown season
- **WHEN** an episode in an `Unknown` season has a review
- **THEN** the episode, its season and its series are kept, and the skip is logged

#### Scenario: Numbers on reload
- **WHEN** the load updates an existing episode
- **THEN** its episode number matches the feed

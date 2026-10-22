//! What a Module's compiled units are: each a function a configuration's
//! step entry calls with its arguments (ADR-0066, amendment 2026-10-22).
//!
//! The owner, 2026-10-22: Prepare, Promote, Demote, Transformation and a
//! Process *are compiled units. They just have to be referred to in the VS
//! Code extension, ending up in the TOML configuration*; a unit is a
//! function, a step entry a call to it with arguments; and of the stages:
//! *Maybe we should use the terms Receive, Process and Send Preparation more
//! clear. The code used in a prepare situation is usually similar. It is
//! just an enum away what it does in what situation.* So a Module's manifest
//! lists its units, each by its name, by what it can be called as (its
//! capability, never read from its name), by the stages it serves — handed
//! the one it is called at, as compress on send and decompress on receive
//! in one unit — and with the settings a call to it gives, in the shape a
//! technology declares a Location's settings in (`xmip-core`'s `settings`):
//!
//! ```toml
//! [[modules.manifest.units]]
//! name = "Decompress"
//! capability = "prepare"
//! stages = ["receive", "send"]
//! offline = true
//!
//! [[modules.manifest.units.settings]]
//! name = "format"
//! kind = "choice"
//! choices = ["gzip", "zstd"]
//! presence = "default"
//! default = "gzip"
//! meaning = "How the Stream is compressed."
//! ```
//!
//! This is the declaration as data; reading a call's settings through it is
//! `xmip-core-configure`'s, which holds it to `xmip-core`'s one reading.

use serde::{Deserialize, Serialize};

/// One compiled unit a Module provides.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleUnit {
    /// Its name, as a step entry's `unit` names it.
    pub name: String,
    /// What it can be called as.
    pub capability: UnitCapability,
    /// Where it may be called, by the stage words of the message path
    /// (`xmip-core-node`'s `Stage`, which reads them): `receive`, `process`,
    /// `send`. It is handed the one it is called at.
    pub stages: Vec<String>,
    /// That it works offline, said explicitly: the owner, 2026-10-22, *We
    /// can only use standards, code and compiled units that explicitly work
    /// offline!* A unit that does not say `true` is refused.
    pub offline: bool,
    /// What a call to it may give, in declared order.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub settings: Vec<UnitSetting>,
}

/// What a unit can be called as.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum UnitCapability {
    /// A Preparation Step, on the Stream.
    Prepare,
    /// Promotion: values out of the content into the Message's context.
    Promote,
    /// Demotion: values out of the context into the content.
    Demote,
    /// A Transformation: one Message's content into another's.
    Transform,
    /// An Xmip Work Process's flow.
    WorkProcess,
    /// Validation, by a format or a custom contract; whether it runs is
    /// still the attaching artifact's `validate`.
    Validate,
}

impl UnitCapability {
    /// Every capability, in the order a person reads them.
    pub const ALL: [Self; 6] = [
        Self::Prepare,
        Self::Promote,
        Self::Demote,
        Self::Transform,
        Self::WorkProcess,
        Self::Validate,
    ];

    /// Its word, as the manifest writes it.
    #[must_use]
    pub const fn word(self) -> &'static str {
        match self {
            Self::Prepare => "prepare",
            Self::Promote => "promote",
            Self::Demote => "demote",
            Self::Transform => "transform",
            Self::WorkProcess => "work-process",
            Self::Validate => "validate",
        }
    }
}

/// One setting a call to a unit may give: `xmip-core`'s `Setting` as data,
/// its kind, bounds, words, presence and default written as that shape
/// crosses (flat), and applying to the call wherever it stands.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct UnitSetting {
    pub name: String,
    /// `text`, `integer`, `boolean`, `duration`, `address`, `secret` or
    /// `choice`.
    pub kind: String,
    /// An integer's least and most.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub minimum: Option<i64>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub maximum: Option<i64>,
    /// A choice's words.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub choices: Vec<String>,
    /// `required`, `optional` or `default`.
    pub presence: String,
    /// The default, where `presence` says `default`: written as TOML
    /// writes the kind, a duration as its text.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub default: Option<UnitDefault>,
    /// What it means, in one sentence.
    #[serde(default)]
    pub meaning: String,
}

/// A unit setting's default, as TOML writes it.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(untagged)]
pub enum UnitDefault {
    Text(String),
    Integer(i64),
    Boolean(bool),
}

#[cfg(test)]
mod tests {
    use super::*;

    #[derive(Deserialize)]
    struct Units {
        units: Vec<ModuleUnit>,
    }

    #[test]
    fn a_unit_reads_its_capability_stages_and_settings() {
        let read: Units = toml::from_str(
            "[[units]]\nname = \"Decompress\"\ncapability = \"prepare\"\n\
             stages = [\"receive\", \"send\"]\noffline = true\n\
             [[units.settings]]\nname = \"format\"\nkind = \"choice\"\n\
             choices = [\"gzip\"]\npresence = \"default\"\ndefault = \"gzip\"\n",
        )
        .expect("reads");
        let unit = &read.units[0];
        assert_eq!(unit.capability, UnitCapability::Prepare);
        assert_eq!(unit.stages, ["receive", "send"]);
        assert_eq!(
            unit.settings[0].default,
            Some(UnitDefault::Text("gzip".into()))
        );
        assert_eq!(UnitCapability::ALL.map(UnitCapability::word)[5], "validate");
    }
}

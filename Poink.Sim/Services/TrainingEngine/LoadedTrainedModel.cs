using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>A saved model rebuilt for training: its options, the network with its saved weights, and the action space those options produce.</summary>
internal sealed record LoadedTrainedModel(TrainedModelSummary Summary, TrainingOptionsModel Options, MlpNetwork Network, ActionSpace ActionSpace);
